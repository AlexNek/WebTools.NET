namespace WebTools.NET.Abstractions;

public interface IBrowserInteraction : IAsyncDisposable
{
    Task<bool> CheckReachabilityAsync(string url, CancellationToken ct = default);

    Task ClickAsync(string selector, CancellationToken ct = default);

    Task FillAsync(string selector, string value, CancellationToken ct = default);

    Task<string> GetContentAsync(CancellationToken ct = default);

    Task<string> GetCurrentUrlAsync(CancellationToken ct = default);

    Task<string> GetHtmlAsync(CancellationToken ct = default);

    /// <summary>Gets the page title.</summary>
    Task<string> GetTitleAsync(CancellationToken ct = default);

    /// <summary>Navigates the browser back one entry in the history.</summary>
    Task GoBackAsync(CancellationToken ct = default);

    /// <summary>Loads browser storage state (cookies, localStorage) from a file.</summary>
    Task LoadStorageStateAsync(string path, CancellationToken ct = default);

    Task NavigateAsync(string url, CancellationToken ct = default);

    /// <summary>Saves browser storage state (cookies, localStorage) to a file.</summary>
    Task SaveStorageStateAsync(string path, CancellationToken ct = default);

    /// <summary>Takes a screenshot and returns it as a base64-encoded PNG string.</summary>
    Task<string> ScreenshotAsync(CancellationToken ct = default);

    /// <summary>Scrolls the page by the given vertical delta in pixels.</summary>
    Task ScrollAsync(int deltaY, CancellationToken ct = default);

    /// <summary>Selects an option in a &lt;select&gt; element by visible text.</summary>
    Task SelectOptionAsync(string selector, string value, CancellationToken ct = default);

    /// <summary>Submits the form that contains the element matching the selector.</summary>
    Task SubmitFormAsync(string selector, CancellationToken ct = default);

    /// <summary>Waits for a CSS selector to appear on the page.</summary>
    Task WaitForSelectorAsync(string selector, int timeoutMs, CancellationToken ct = default);
}
