namespace WebTools.NET.Abstractions;

/// <summary>
/// Minimal browser interface for content fetching and navigation.
/// Consumers that only need to read page content depend on this interface.
/// </summary>
public interface IBrowserContent : IAsyncDisposable
{
    Task<bool> CheckReachabilityAsync(string url, CancellationToken ct = default);

    Task<string> GetContentAsync(CancellationToken ct = default);

    Task<string> GetCurrentUrlAsync(CancellationToken ct = default);

    Task<string> GetHtmlAsync(CancellationToken ct = default);

    /// <summary>Gets the page title.</summary>
    Task<string> GetTitleAsync(CancellationToken ct = default);

    Task NavigateAsync(string url, CancellationToken ct = default);
}
