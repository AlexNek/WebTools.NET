using WebTools.NET.Models;

namespace WebTools.NET.Abstractions;

public interface IWebAccessService
{
    Task<UrlCheckResult> CheckReachabilityAsync(string url, CancellationToken ct = default);
}
