using Microsoft.Playwright;

using WebTools.NET.Browsing;

namespace WebTools.NET.Tests;

public sealed class TimeoutBrowserContentFetcher : BrowserContentFetcherBase
{
    protected override string BrowserNotInstalledMessage => "Test browser is unavailable.";

    protected override Task<IBrowserContext> CreateContextAsync(CancellationToken ct) =>
        throw new TimeoutException("Test navigation timed out.");

    protected override Task DisposeBrowserResourcesAsync() => Task.CompletedTask;
}
