using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET;

/// <summary>
/// Legacy browser facade retained for source compatibility.
/// Use <see cref="BrowserSession"/> and <see cref="BrowserOperation"/> for new code.
/// </summary>
[Obsolete("Use BrowserSession instead.")]
public sealed class BrowserAgent : IAsyncDisposable
{
    private readonly BrowserSession _session;

    public BrowserAgent(
        IBrowserAgentInteraction browser,
        BrowserAgentOptions? options = null,
        ILogger<BrowserAgent>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(browser);
        _ = logger;
        _session = new BrowserSession(browser, ToSessionOptions(options));
    }

    /// <summary>Legacy action history mapped from the current operation history.</summary>
    public IReadOnlyList<BrowserAction> ActionHistory =>
        _session.OperationHistory.Select(ToBrowserAction).ToList().AsReadOnly();

    public ValueTask DisposeAsync() => _session.DisposeAsync();

    public async Task<PageSnapshot> ExecuteAsync(BrowserAction action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ToPageSnapshot(await _session.ExecuteAsync(ToBrowserOperation(action), ct).ConfigureAwait(false));
    }

    public async Task<PageSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
        ToPageSnapshot(await _session.GetSnapshotAsync(ct).ConfigureAwait(false));

    public async Task<PageSnapshot> StartAsync(
        string url,
        EContentFormat? format = null,
        CancellationToken ct = default) =>
        ToPageSnapshot(await _session.StartAsync(url, format, ct).ConfigureAwait(false));

    private static BrowserSessionOptions ToSessionOptions(BrowserAgentOptions? options)
    {
        options ??= new BrowserAgentOptions();
        return new BrowserSessionOptions
        {
            MaxOperations = options.MaxActions,
            MaxDuration = options.MaxDuration,
            DefaultFormat = options.DefaultFormat,
            IncludeScreenshot = options.IncludeScreenshot,
            StorageStatePath = options.StorageStatePath
        };
    }

    private static BrowserOperation ToBrowserOperation(BrowserAction action) => new(
        ToBrowserOperationType(action.Type),
        action.ElementIndex,
        action.Value,
        action.Fields,
        action.TimeoutMs);

    private static BrowserAction ToBrowserAction(BrowserOperation operation) => new(
        ToBrowserActionType(operation.Type),
        operation.ElementIndex,
        operation.Value,
        operation.Fields,
        operation.TimeoutMs);

    private static EBrowserOperationType ToBrowserOperationType(EBrowserActionType type) => type switch
    {
        EBrowserActionType.Navigate => EBrowserOperationType.Navigate,
        EBrowserActionType.Click => EBrowserOperationType.Click,
        EBrowserActionType.Fill => EBrowserOperationType.Fill,
        EBrowserActionType.FillForm => EBrowserOperationType.FillForm,
        EBrowserActionType.Select => EBrowserOperationType.Select,
        EBrowserActionType.Submit => EBrowserOperationType.Submit,
        EBrowserActionType.ScrollDown => EBrowserOperationType.ScrollDown,
        EBrowserActionType.ScrollUp => EBrowserOperationType.ScrollUp,
        EBrowserActionType.WaitFor => EBrowserOperationType.WaitFor,
        EBrowserActionType.Back => EBrowserOperationType.Back,
        EBrowserActionType.Snapshot => EBrowserOperationType.Snapshot,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown browser action type.")
    };

    private static EBrowserActionType ToBrowserActionType(EBrowserOperationType type) => type switch
    {
        EBrowserOperationType.Navigate => EBrowserActionType.Navigate,
        EBrowserOperationType.Click => EBrowserActionType.Click,
        EBrowserOperationType.Fill => EBrowserActionType.Fill,
        EBrowserOperationType.FillForm => EBrowserActionType.FillForm,
        EBrowserOperationType.Select => EBrowserActionType.Select,
        EBrowserOperationType.Submit => EBrowserActionType.Submit,
        EBrowserOperationType.ScrollDown => EBrowserActionType.ScrollDown,
        EBrowserOperationType.ScrollUp => EBrowserActionType.ScrollUp,
        EBrowserOperationType.WaitFor => EBrowserActionType.WaitFor,
        EBrowserOperationType.Back => EBrowserActionType.Back,
        EBrowserOperationType.Snapshot => EBrowserActionType.Snapshot,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown browser operation type.")
    };

    private static PageSnapshot ToPageSnapshot(BrowserSnapshot snapshot) => new(
        snapshot.Url,
        snapshot.Title,
        snapshot.Content,
        snapshot.Elements,
        snapshot.Format,
        snapshot.StatusCode,
        snapshot.Error,
        snapshot.HasMoreContent,
        snapshot.ScreenshotBase64);
}
