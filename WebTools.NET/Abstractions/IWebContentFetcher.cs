using WebTools.NET.Models;

namespace WebTools.NET.Abstractions;

public interface IWebContentFetcher : IAsyncDisposable
{
    Task<UrlCheckResult> CheckReachabilityAsync(string url, CancellationToken ct = default);

    Task<WebContent> FetchAsync(string url, CancellationToken ct = default);
}
