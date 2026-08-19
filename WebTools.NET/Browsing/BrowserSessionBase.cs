using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Shared implementation for browser session classes.
/// Subclasses provide only the page initialization logic via <see cref="CreatePageAsync"/>.
/// </summary>
public abstract class BrowserSessionBase : IBrowserSession, IBrowserSessionLifecycle, IBrowserSessionState
{
    private const int ClickTimeoutMs = 5000;

    private const int NavigateTimeoutMs = 15000;

    private const int NetworkIdleTimeoutMs = 10000;

    private const int ReachabilityTimeoutMs = 10000;

    private const int ScrollNetworkIdleMs = 3000;

    private const int WaitForSelectorDefaultMs = 5000;

    private const int UnknownNavigationStatus = 0;

    internal const int DefaultViewportHeight = 1080;

    internal const int DefaultViewportWidth = 1920;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private readonly Lazy<Task> _disposeTask;

    private IBrowserContext? _context;

    private int _disposed;

    private int _lastNavigationStatus = UnknownNavigationStatus;

    private IPage? _page;

    protected BrowserSessionBase(
        string? storageStatePath = null,
        BrowserSessionOptions? options = null)
    {
        StorageStatePath = storageStatePath;
        SessionOptions = options ?? new BrowserSessionOptions();
        _disposeTask = new Lazy<Task>(DisposeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Storage state path used when the first browser context is created.</summary>
    protected string? StorageStatePath { get; private set; }

    /// <summary>Session viewport and context configuration.</summary>
    protected BrowserSessionOptions SessionOptions { get; }

    /// <inheritdoc />
    public bool IsPageReady =>
        Volatile.Read(ref _disposed) == 0 && _page is not null && _context is not null;

    public async Task<bool> CheckReachabilityAsync(string url, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _lastNavigationStatus, UnknownNavigationStatus);
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var response = await page.GotoAsync(
                    url,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = ReachabilityTimeoutMs
                    })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            var status = response?.Status ?? UnknownNavigationStatus;
            Volatile.Write(ref _lastNavigationStatus, response?.Status ?? UnknownNavigationStatus);
            if (HttpStatusHelper.IsNotSuccess(status))
            {
                return false;
            }

            var finalUrl = page.Url;
            return !HtmlUtils.IsErrorPageUrl(finalUrl);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            await page.ClickAsync(selector, new PageClickOptions { Timeout = ClickTimeoutMs })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await WaitForNetworkIdleAsync(page, NetworkIdleTimeoutMs, ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public ValueTask DisposeAsync() => new(_disposeTask.Value);

    public async Task ResetAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _initLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                await ClosePageAndContextAsync().ConfigureAwait(false);
                Volatile.Write(ref _lastNavigationStatus, UnknownNavigationStatus);
            }
            finally
            {
                _initLock.Release();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task DisposeCoreAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _initLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await ClosePageAndContextAsync().ConfigureAwait(false);
                await DisposeResourcesAsync().ConfigureAwait(false);
            }
            finally
            {
                _initLock.Release();
            }
        }
        finally
        {
            _operationLock.Release();
            _initLock.Dispose();
            _operationLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async Task ClosePageAndContextAsync()
    {
        var page = _page;
        _page = null;
        var context = _context;
        _context = null;

        if (page is not null)
        {
            page.Response -= TrackDocumentResponse;
            try
            {
                await page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
            {
            }
        }

        await CloseContextAsync(context).ConfigureAwait(false);
    }

    public async Task FillAsync(string selector, string value, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            await page.FillAsync(selector, value).AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<string> GetContentAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var body = await page.TextContentAsync("body").AwaitWithCancellationAsync(ct).ConfigureAwait(false) ?? "";
            ct.ThrowIfCancellationRequested();
            return body;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<string> GetCurrentUrlAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            return page.Url;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<string> GetHtmlAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var html = await page.ContentAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return html;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<IReadOnlyList<InteractiveElement>> GetInteractiveElementsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var rawElements = await page.EvaluateAsync<ElementExtractionResult[]>(InteractiveElementsScript.Script)
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var elements = new List<InteractiveElement>(rawElements.Length);
            var index = 1;
            foreach (var raw in rawElements)
            {
                try
                {
                    var selector = raw.Selector;
                    if (string.IsNullOrWhiteSpace(selector) ||
                        await page.Locator(selector).CountAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false) != 1)
                    {
                        continue;
                    }

                    elements.Add(new InteractiveElement(
                        Index: index++,
                        Tag: raw.Tag,
                        Type: raw.Type,
                        Text: raw.Text,
                        Href: raw.Href,
                        Name: raw.Name,
                        Selector: selector));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (PlaywrightException)
                {
                    // Omit selectors that are invalid or no longer resolve uniquely.
                }
            }

            return elements;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<int?> GetLastNavigationStatusAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var status = Volatile.Read(ref _lastNavigationStatus);
            return status == UnknownNavigationStatus ? null : status;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<string> GetTitleAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var title = await page.TitleAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return title;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<int> GetViewportHeightAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return SessionOptions.ViewportHeight;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task GoBackAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _lastNavigationStatus, UnknownNavigationStatus);
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var response = await page.GoBackAsync(new PageGoBackOptions { Timeout = NavigateTimeoutMs })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            if (response?.Status is int status)
            {
                Volatile.Write(ref _lastNavigationStatus, status);
            }
            ct.ThrowIfCancellationRequested();
            await WaitForNetworkIdleAsync(page, NetworkIdleTimeoutMs, ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<bool> HasMoreContentAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var result = await page.EvaluateAsync<bool>(
                    "() => { const scroller = document.scrollingElement || document.documentElement; return scroller.scrollHeight > window.scrollY + window.innerHeight + 50; }")
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<bool> IsCheckedAsync(string selector, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var result = await page.IsCheckedAsync(selector).AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task LoadStorageStateAsync(string path, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_page is not null)
            {
                throw new InvalidOperationException(
                    "Storage state must be loaded before the browser session creates its first page.");
            }

            StorageStatePath = File.Exists(path) ? path : null;
            await Task.CompletedTask.ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task NavigateAsync(string url, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _lastNavigationStatus, UnknownNavigationStatus);
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var response = await page.GotoAsync(
                    url,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = NavigateTimeoutMs
                    })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);

            Volatile.Write(ref _lastNavigationStatus, response?.Status ?? UnknownNavigationStatus);
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SaveStorageStateAsync(string path, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            await page.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = path })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<string> ScreenshotAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = false })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return Convert.ToBase64String(bytes);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task ScrollAsync(int deltaY, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, deltaY).AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await WaitForNetworkIdleAsync(page, ScrollNetworkIdleMs, ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SelectOptionAsync(string selector, string value, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            await page.SelectOptionAsync(selector, new SelectOptionValue { Label = value })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SubmitFormAsync(string selector, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var submitted = await page.EvalOnSelectorAsync<bool>(
                    selector,
                    "el => { const form = el.closest('form'); " +
                    "if (!form) return false; " +
                    "const submitter = el.matches('button, input[type=submit], input[type=image]') ? el : undefined; " +
                    "form.requestSubmit(submitter); return true; }")
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);

            if (!submitted)
            {
                throw new InvalidOperationException($"Element '{selector}' is not contained in a form.");
            }

            ct.ThrowIfCancellationRequested();
            await WaitForNetworkIdleAsync(page, NetworkIdleTimeoutMs, ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task WaitForSelectorAsync(string selector, int timeoutMs, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ct.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var page = await GetPageAsync(ct).ConfigureAwait(false);
            var timeout = timeoutMs > 0 ? timeoutMs : WaitForSelectorDefaultMs;
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = timeout })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    protected static async Task CloseContextAsync(IBrowserContext? context)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            await context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
        {
        }
    }

    protected BrowserNewContextOptions CreateContextOptions(string? storageStatePath)
    {
        var options = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = SessionOptions.ViewportWidth,
                Height = SessionOptions.ViewportHeight
            }
        };

        if (storageStatePath is not null && File.Exists(storageStatePath))
        {
            options.StorageStatePath = storageStatePath;
        }

        return options;
    }

    /// <summary>
    /// Creates and returns the browser context and Playwright page. Called once during lazy initialization.
    /// Subclasses implement browser-specific launch and context creation here.
    /// </summary>
    protected abstract Task<(IBrowserContext Context, IPage Page)> CreatePageAsync(CancellationToken ct);

    /// <summary>
    /// Disposes browser-specific resources (browser instance, playwright, handles).
    /// Called from <see cref="DisposeAsync"/> after page is closed.
    /// </summary>
    protected abstract Task DisposeResourcesAsync();

    private async Task<IPage> GetPageAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        if (_page is not null)
        {
            return _page;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_page is not null)
            {
                return _page;
            }

            var created = await CreatePageAsync(ct).ConfigureAwait(false);
            try
            {
                created.Page.Response += TrackDocumentResponse;
                _page = created.Page;
                _context = created.Context;
                return created.Page;
            }
            catch
            {
                try
                {
                    await created.Page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
                {
                }

                await CloseContextAsync(created.Context).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task WaitForNetworkIdleAsync(IPage page, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await page.WaitForLoadStateAsync(
                    LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = timeoutMs })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        catch (TimeoutException)
        {
            ct.ThrowIfCancellationRequested();
        }
    }

    private void TrackDocumentResponse(object? sender, IResponse response)
    {
        try
        {
            if (response.Frame == _page?.MainFrame &&
                string.Equals(response.Request.ResourceType, "document", StringComparison.OrdinalIgnoreCase))
            {
                Volatile.Write(ref _lastNavigationStatus, response.Status);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
