using System.Diagnostics;

using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET;

/// <summary>
/// Stateful browser session that lets any external caller navigate, interact with,
/// and extract information from web pages across multiple turns.
/// </summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private const int DisposeTimeoutMs = 5000;

    private readonly IBrowserAgentInteraction _browser;

    private readonly List<BrowserOperation> _history = [];

    private readonly ILogger<BrowserSession>? _logger;

    private readonly BrowserSessionOptions _options;

    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private readonly Lazy<Task> _disposeTask;

    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly Stopwatch _sessionTimer = new();

    private EContentFormat _format;

    private BrowserSnapshot? _lastSnapshot;

    private bool _started;

    private int _disposed;

    /// <summary>
    /// Creates a browser session over an explicitly created browser session.
    /// The caller remains responsible for creating and disposing the session.
    /// Use <see cref="IBrowserSessionFactory"/> to create one isolated session
    /// per independent workflow.
    /// </summary>
    public BrowserSession(
        IBrowserSession browser,
        BrowserSessionOptions? options = null,
        ILogger<BrowserSession>? logger = null)
        : this((IBrowserAgentInteraction)browser, options, logger)
    {
    }

    internal BrowserSession(
        IBrowserAgentInteraction browser,
        BrowserSessionOptions? options = null,
        ILogger<BrowserSession>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(browser);
        _browser = browser;
        _options = options ?? new BrowserSessionOptions();
        _format = _options.DefaultFormat;
        _logger = logger;
        _disposeTask = new Lazy<Task>(DisposeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>History of operations executed in the current session.</summary>
    public IReadOnlyList<BrowserOperation> OperationHistory => _history.AsReadOnly();

    public ValueTask DisposeAsync() => new(_disposeTask.Value);

    private async Task DisposeCoreAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdownCts.Cancel();

        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_started && _options.StorageStatePath is not null)
            {
                try
                {
                    await _browser.SaveStorageStateAsync(_options.StorageStatePath, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException)
                {
                    _logger?.LogWarning("Storage state save skipped because the session was already disposed.");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to save storage state to {Path}.", _options.StorageStatePath);
                }
            }
        }
        finally
        {
            _operationLock.Release();
            _operationLock.Dispose();
            _shutdownCts.Dispose();
        }
    }

    /// <summary>Executes a browser operation and returns the updated page snapshot.</summary>
    public async Task<BrowserSnapshot> ExecuteAsync(BrowserOperation operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await ExecuteCoreAsync(operation, ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<BrowserSnapshot> ExecuteCoreAsync(BrowserOperation operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        if (!_started)
        {
            return ErrorSnapshot("Session not started. Call StartAsync first.");
        }

        if (_history.Count >= _options.MaxOperations)
        {
            return await ErrorSnapshotAsync(
                $"Operation limit ({_options.MaxOperations}) reached.",
                ct).ConfigureAwait(false);
        }

        var remaining = _options.MaxDuration - _sessionTimer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return LastSnapshotWithError(GetDurationLimitError());
        }

        var validationError = ValidateOperation(operation);
        if (validationError is not null)
        {
            return await ErrorSnapshotAsync(validationError, ct).ConfigureAwait(false);
        }

        _history.Add(operation);
        _logger?.LogDebug("Executing operation #{Count}: {Type}", _history.Count, operation.Type);

        using var durationCts = new CancellationTokenSource(remaining);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            durationCts.Token,
            _shutdownCts.Token);
        var operationToken = operationCts.Token;

        try
        {
            var operationTask = DispatchOperationAsync(operation, operationToken);
            await AwaitSessionOperationAsync(operationTask, operationToken, ct, durationCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (durationCts.IsCancellationRequested)
        {
            _logger?.LogDebug("Operation {Type} exceeded the session duration limit.", operation.Type);
            return LastSnapshotWithError(GetDurationLimitError());
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Operation {Type} failed: {Error}", operation.Type, ex.Message);
            var snapshotToken = durationCts.IsCancellationRequested
                ? CancellationToken.None
                : operationToken;
            return await BuildSnapshotAsync(NormalizeError(ex), ct, snapshotToken)
                .ConfigureAwait(false);
        }

        var snapshot = await BuildSnapshotAsync(null, ct, operationToken).ConfigureAwait(false);
        return ApplyDurationLimit(snapshot);
    }

    /// <summary>Returns the current page state without performing an operation.</summary>
    public async Task<BrowserSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await GetSnapshotCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<BrowserSnapshot> GetSnapshotCoreAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (!_started)
        {
            return ErrorSnapshot("Session not started. Call StartAsync first.");
        }

        var remaining = _options.MaxDuration - _sessionTimer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return LastSnapshotWithError(GetDurationLimitError());
        }

        using var durationCts = new CancellationTokenSource(remaining);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            durationCts.Token,
            _shutdownCts.Token);
        var snapshot = await BuildSnapshotAsync(null, ct, operationCts.Token).ConfigureAwait(false);
        return ApplyDurationLimit(snapshot);
    }

    /// <summary>Starts a new browser session session by navigating to the given URL.</summary>
    public async Task<BrowserSnapshot> StartAsync(
        string url,
        EContentFormat? format = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await StartCoreAsync(url, format, ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<BrowserSnapshot> StartCoreAsync(
        string url,
        EContentFormat? format,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        var isFirstStart = !_started;
        _history.Clear();
        _lastSnapshot = null;
        _format = format ?? _options.DefaultFormat;
        _sessionTimer.Restart();

        _logger?.LogDebug("Starting browser session session at {Url}", url);

        using var durationCts = new CancellationTokenSource(_options.MaxDuration);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            durationCts.Token,
            _shutdownCts.Token);
        var operationToken = operationCts.Token;

        try
        {
            if (isFirstStart && _options.StorageStatePath is not null)
            {
                try
                {
                    var loadOperation = _browser.LoadStorageStateAsync(
                        _options.StorageStatePath,
                        operationToken);
                    await AwaitSessionOperationAsync(loadOperation, operationToken, ct, durationCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _started = false;
                    _logger?.LogDebug("Loading storage state failed: {Error}", ex.Message);
                    await ResetBrowserAfterFailedStartAsync().ConfigureAwait(false);
                    return ErrorSnapshot(NormalizeError(ex));
                }
            }

            var navigationOperation = _browser.NavigateAsync(url, operationToken);
            await AwaitSessionOperationAsync(navigationOperation, operationToken, ct, durationCts.Token)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            _started = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _started = false;
            await ResetBrowserAfterFailedStartAsync().ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            _started = false;
            await ResetBrowserAfterFailedStartAsync().ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException) when (durationCts.IsCancellationRequested)
        {
            _started = false;
            await ResetBrowserAfterFailedStartAsync().ConfigureAwait(false);
            return ErrorSnapshot(GetDurationLimitError());
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Navigation to {Url} failed: {Error}", url, ex.Message);
            if (!HasUsablePage())
            {
                _started = false;
                await ResetBrowserAfterFailedStartAsync().ConfigureAwait(false);
                return ErrorSnapshot(NormalizeError(ex));
            }

            _started = true;
            return await BuildSnapshotAsync(NormalizeError(ex), ct, operationToken)
                .ConfigureAwait(false);
        }

        return ApplyDurationLimit(
            await BuildSnapshotAsync(null, ct, operationToken).ConfigureAwait(false));
    }

    private async Task<BrowserSnapshot> BuildSnapshotAsync(
        string? error,
        CancellationToken callerToken,
        CancellationToken operationToken)
    {
        var url = "";
        var title = "";
        var html = "";
        var content = "";
        IReadOnlyList<InteractiveElement> elements = [];
        var hasMore = false;
        int? statusCode = null;
        string? screenshot = null;
        string? snapshotError = null;
        var deadlineExceeded = false;

        void RecordSnapshotError(Exception ex)
        {
            snapshotError ??= NormalizeError(ex);
            _logger?.LogDebug("Failed to build part of snapshot: {Error}", ex.Message);
        }

        async Task CaptureAsync<T>(Func<Task<T>> operation, Action<T> assign, bool reportFailure = true)
        {
            try
            {
                assign(await AwaitSessionOperationAsync(
                        operation(),
                        operationToken,
                        callerToken,
                        operationToken)
                    .ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
            {
                deadlineExceeded = true;
            }
            catch (Exception ex) when (reportFailure)
            {
                RecordSnapshotError(ex);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Optional snapshot component was unavailable: {Error}", ex.Message);
            }
        }

        callerToken.ThrowIfCancellationRequested();
        await CaptureAsync(
            () => _browser.GetCurrentUrlAsync(operationToken),
            value => url = value).ConfigureAwait(false);
        await CaptureAsync(
            () => _browser.GetLastNavigationStatusAsync(operationToken),
            value => statusCode = value).ConfigureAwait(false);
        await CaptureAsync(
            () => _browser.GetTitleAsync(operationToken),
            value => title = value).ConfigureAwait(false);
        await CaptureAsync(
            () => _browser.GetHtmlAsync(operationToken),
            value => html = value).ConfigureAwait(false);

        try
        {
            operationToken.ThrowIfCancellationRequested();
            content = ContentProcessor.Process(html, _format, null, ESanitizeLevel.Minimal);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            deadlineExceeded = true;
        }
        catch (Exception ex)
        {
            RecordSnapshotError(ex);
        }

        await CaptureAsync(
            () => _browser.GetInteractiveElementsAsync(operationToken),
            value => elements = value).ConfigureAwait(false);
        await CaptureAsync(
            () => _browser.HasMoreContentAsync(operationToken),
            value => hasMore = value).ConfigureAwait(false);

        if (_options.IncludeScreenshot)
        {
            await CaptureAsync(
                () => _browser.ScreenshotAsync(operationToken),
                value => screenshot = value,
                reportFailure: false).ConfigureAwait(false);
        }

        callerToken.ThrowIfCancellationRequested();

        var snapshot = new BrowserSnapshot(
            Url: url,
            Title: title,
            Content: content,
            Elements: elements,
            Format: _format,
            StatusCode: statusCode,
            Error: error ?? GetStatusError(statusCode) ??
                (deadlineExceeded ? GetDurationLimitError() : snapshotError),
            HasMoreContent: hasMore,
            ScreenshotBase64: screenshot);
        _lastSnapshot = snapshot;
        return snapshot;
    }

    private async Task DispatchOperationAsync(BrowserOperation operation, CancellationToken ct)
    {
        switch (operation.Type)
        {
            case EBrowserOperationType.Navigate:
                await _browser.NavigateAsync(operation.Value!, ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.Click:
                await _browser.ClickAsync(await ResolveElementSelectorAsync(operation.ElementIndex!.Value, ct), ct)
                    .ConfigureAwait(false);
                break;
            case EBrowserOperationType.Fill:
                await _browser.FillAsync(
                    await ResolveElementSelectorAsync(operation.ElementIndex!.Value, ct),
                    operation.Value ?? "",
                    ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.FillForm:
                await ExecuteFillFormAsync(operation.Fields!, ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.Select:
                await _browser.SelectOptionAsync(
                    await ResolveElementSelectorAsync(operation.ElementIndex!.Value, ct),
                    operation.Value ?? "",
                    ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.Submit:
                await _browser.SubmitFormAsync(
                    await ResolveElementSelectorAsync(operation.ElementIndex!.Value, ct), ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.ScrollDown:
                await _browser.ScrollAsync(
                    await GetViewportHeightAsync(ct).ConfigureAwait(false),
                    ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.ScrollUp:
                await _browser.ScrollAsync(
                    -await GetViewportHeightAsync(ct).ConfigureAwait(false),
                    ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.WaitFor:
                await _browser.WaitForSelectorAsync(operation.Value!, operation.TimeoutMs ?? 5000, ct)
                    .ConfigureAwait(false);
                break;
            case EBrowserOperationType.Back:
                await _browser.GoBackAsync(ct).ConfigureAwait(false);
                break;
            case EBrowserOperationType.Snapshot:
                break;
            default:
                throw new InvalidOperationException($"Unknown operation type: {operation.Type}");
        }
    }

    private async Task AwaitSessionOperationAsync(
        Task operation,
        CancellationToken operationToken,
        CancellationToken callerToken,
        CancellationToken durationToken)
    {
        if (_browser is not IBrowserSessionLifecycle)
        {
            await operation.AwaitWithCancellationAsync(operationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await operation.WaitAsync(operationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            if (callerToken.IsCancellationRequested)
            {
                await operation.AwaitWithCancellationAsync(operationToken).ConfigureAwait(false);
                return;
            }

            if (durationToken.IsCancellationRequested || _shutdownCts.IsCancellationRequested)
            {
                await AbortBrowserOperationAsync(operation).ConfigureAwait(false);
            }

            operationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<T> AwaitSessionOperationAsync<T>(
        Task<T> operation,
        CancellationToken operationToken,
        CancellationToken callerToken,
        CancellationToken durationToken)
    {
        if (_browser is not IBrowserSessionLifecycle)
        {
            return await operation.AwaitWithCancellationAsync(operationToken).ConfigureAwait(false);
        }

        try
        {
            return await operation.WaitAsync(operationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            if (callerToken.IsCancellationRequested)
            {
                return await operation.AwaitWithCancellationAsync(operationToken).ConfigureAwait(false);
            }

            if (durationToken.IsCancellationRequested || _shutdownCts.IsCancellationRequested)
            {
                await AbortBrowserOperationAsync(operation).ConfigureAwait(false);
            }

            operationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task AbortBrowserOperationAsync(Task operation)
    {
        if (_browser is not IBrowserSessionLifecycle lifecycle)
        {
            return;
        }

        try
        {
            using var resetCts = new CancellationTokenSource(DisposeTimeoutMs);
            await lifecycle.ResetAsync(resetCts.Token).WaitAsync(resetCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or ObjectDisposedException)
        {
            _logger?.LogWarning("Browser reset did not complete after an interrupted operation.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to reset the browser after an interrupted operation.");
        }

        try
        {
            using var observeCts = new CancellationTokenSource(DisposeTimeoutMs);
            await operation.WaitAsync(observeCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "The interrupted browser operation completed with an error after reset.");
        }
    }

    private bool HasUsablePage() =>
        _browser is not IBrowserSessionState state || state.IsPageReady;

    private BrowserSnapshot LastSnapshotWithError(string error) =>
        _lastSnapshot is null
            ? ErrorSnapshot(error)
            : _lastSnapshot with { Error = error };

    private BrowserSnapshot ErrorSnapshot(string error) => new(
        Url: "",
        Title: "",
        Content: "",
        Elements: [],
        Format: _format,
        StatusCode: null,
        Error: error);

    private async Task<BrowserSnapshot> ErrorSnapshotAsync(string error, CancellationToken ct)
    {
        if (!_started)
        {
            return ErrorSnapshot(error);
        }

        var remaining = _options.MaxDuration - _sessionTimer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return LastSnapshotWithError(error);
        }

        using var recoveryCts = new CancellationTokenSource(remaining);
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            recoveryCts.Token,
            _shutdownCts.Token);
        return await BuildSnapshotAsync(error, ct, operationCts.Token).ConfigureAwait(false);
    }

    private async Task ResetBrowserAfterFailedStartAsync()
    {
        if (_browser is not IBrowserSessionLifecycle lifecycle)
        {
            return;
        }

        try
        {
            await lifecycle.ResetAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to reset the browser after startup cancellation.");
        }
    }

    private async Task<int> GetViewportHeightAsync(CancellationToken ct)
    {
        var height = await _browser.GetViewportHeightAsync(ct).ConfigureAwait(false);
        return height > 0 ? height : BrowserSessionBase.DefaultViewportHeight;
    }

    private async Task ExecuteFillFormAsync(IReadOnlyList<FormFieldValue> fields, CancellationToken ct)
    {
        var elements = await _browser.GetInteractiveElementsAsync(ct).ConfigureAwait(false);
        var elementsByIndex = elements.ToDictionary(element => element.Index);
        var resolvedFields = new List<(InteractiveElement Element, string Value)>(fields.Count);
        var usedIndexes = new HashSet<int>();

        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(field.Value);
            ct.ThrowIfCancellationRequested();

            if (field.ElementIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(field.ElementIndex), field.ElementIndex,
                    "ElementIndex must be greater than zero.");
            }

            if (!usedIndexes.Add(field.ElementIndex))
            {
                throw new InvalidOperationException(
                    $"Element index {field.ElementIndex} occurs more than once in FillForm.");
            }

            if (!elementsByIndex.TryGetValue(field.ElementIndex, out var element))
            {
                throw new InvalidOperationException($"Element index {field.ElementIndex} not found.");
            }

            var tag = element.Tag.ToLowerInvariant();
            var type = element.Type?.ToLowerInvariant();

            if (tag == "input" && type == "checkbox")
            {
                if (!string.Equals(field.Value, "true", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(field.Value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "Checkbox values must be either 'true' or 'false'.",
                        nameof(field.Value));
                }
            }
            else if (tag is "input" or "textarea")
            {
                if (type is "button" or "submit" or "reset" or "image" or "file" or "radio")
                {
                    throw new InvalidOperationException(
                        $"Element index {field.ElementIndex} is not a fillable control.");
                }
            }
            else if (tag != "select")
            {
                throw new InvalidOperationException(
                    $"Element index {field.ElementIndex} is not a fillable control.");
            }

            resolvedFields.Add((element, field.Value));
        }

        foreach (var (element, value) in resolvedFields)
        {
            var tag = element.Tag.ToLowerInvariant();
            var type = element.Type?.ToLowerInvariant();

            if (tag == "select")
            {
                await _browser.SelectOptionAsync(element.Selector, value, ct).ConfigureAwait(false);
            }
            else if (tag == "input" && type == "checkbox")
            {
                var shouldCheck = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                var isCurrentlyChecked = await _browser.IsCheckedAsync(element.Selector, ct).ConfigureAwait(false);
                if (shouldCheck != isCurrentlyChecked)
                {
                    await _browser.ClickAsync(element.Selector, ct).ConfigureAwait(false);
                }
            }
            else
            {
                await _browser.FillAsync(element.Selector, value, ct).ConfigureAwait(false);
            }
        }
    }

    private BrowserSnapshot ApplyDurationLimit(BrowserSnapshot snapshot) =>
        _sessionTimer.Elapsed >= _options.MaxDuration
            ? snapshot with { Error = GetDurationLimitError() }
            : snapshot;

    private string GetDurationLimitError() =>
        $"Session duration limit ({_options.MaxDuration:g}) exceeded.";

    private static string? GetStatusError(int? statusCode) =>
        statusCode is >= 400 ? $"HTTP {statusCode}" : null;

    private static string NormalizeError(Exception ex)
    {
        var message = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
        const string playwrightPrefix = "Microsoft.Playwright.";
        if (message.StartsWith(playwrightPrefix, StringComparison.Ordinal))
        {
            message = message[playwrightPrefix.Length..];
        }

        const int maxLength = 300;
        return message.Length > maxLength
            ? string.Concat(message.AsSpan(0, maxLength), "...")
            : message;
    }

    private async Task<string> ResolveElementSelectorAsync(int elementIndex, CancellationToken ct)
    {
        var elements = await _browser.GetInteractiveElementsAsync(ct).ConfigureAwait(false);
        return elements.FirstOrDefault(e => e.Index == elementIndex)?.Selector
            ?? throw new InvalidOperationException($"Element index {elementIndex} not found.");
    }

    private static string? ValidateOperation(BrowserOperation operation)
    {
        return operation.Type switch
        {
            EBrowserOperationType.Navigate when string.IsNullOrWhiteSpace(operation.Value)
                => "Navigate requires a URL in Value.",
            EBrowserOperationType.Click when operation.ElementIndex is null or <= 0
                => "Click requires a positive ElementIndex.",
            EBrowserOperationType.Fill when operation.ElementIndex is null or <= 0
                => "Fill requires a positive ElementIndex.",
            EBrowserOperationType.Select when operation.ElementIndex is null or <= 0
                => "Select requires a positive ElementIndex.",
            EBrowserOperationType.Submit when operation.ElementIndex is null or <= 0
                => "Submit requires a positive ElementIndex.",
            EBrowserOperationType.FillForm when operation.Fields is null or { Count: 0 }
                => "FillForm requires at least one field in Fields.",
            EBrowserOperationType.WaitFor when string.IsNullOrWhiteSpace(operation.Value)
                => "WaitFor requires a CSS selector in Value.",
            EBrowserOperationType.WaitFor when operation.TimeoutMs is <= 0
                => "WaitFor TimeoutMs must be positive when specified.",
            _ => null
        };
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(BrowserSession));
        }
    }
}
