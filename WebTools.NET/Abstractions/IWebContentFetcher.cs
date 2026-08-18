using WebTools.NET.Models;

namespace WebTools.NET.Abstractions;

public interface IWebContentFetcher : IAsyncDisposable
{
    Task<UrlCheckResult> CheckReachabilityAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Fetches the textual content of the given URL.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="maxContentLength">
    /// Maximum number of characters to return. When <c>null</c> (default), the full
    /// content is returned without truncation. When specified, the content is truncated
    /// to this many characters.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fetched web content.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxContentLength"/> is less than or equal to zero.
    /// </exception>
    Task<WebContent> FetchAsync(string url, int? maxContentLength = null, CancellationToken ct = default);

    /// <summary>
    /// Fetches the content of the given URL in the specified format.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="format">Output format: PlainText, Markdown, or Html.</param>
    /// <param name="maxContentLength">
    /// Maximum number of characters to return. When <c>null</c>, the full
    /// content is returned without truncation.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fetched web content in the requested format.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxContentLength"/> is less than or equal to zero.
    /// </exception>
    Task<WebContent> FetchAsAsync(string url, EContentFormat format, int? maxContentLength = null, CancellationToken ct = default);
}
