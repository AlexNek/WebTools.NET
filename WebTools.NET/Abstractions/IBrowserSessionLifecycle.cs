namespace WebTools.NET.Abstractions;

/// <summary>
/// Optional lifecycle capability for browser sessions used by <see cref="WebTools.NET.BrowserSession"/>.
/// </summary>
public interface IBrowserSessionLifecycle
{
    /// <summary>
    /// Closes the current page and browser context while retaining the browser process,
    /// allowing a subsequent start to load storage state again.
    /// </summary>
    Task ResetAsync(CancellationToken ct = default);
}
