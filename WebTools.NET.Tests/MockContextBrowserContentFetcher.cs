using Microsoft.Playwright;

using WebTools.NET.Browsing;

namespace WebTools.NET.Tests;

/// <summary>
/// Test fetcher that returns a caller-supplied (mocked) browser context, allowing a test to
/// drive the full navigate-then-fetch flow of <see cref="BrowserContentFetcherBase"/> without a
/// real browser.
/// </summary>
public sealed class MockContextBrowserContentFetcher : BrowserContentFetcherBase
{
    private readonly IBrowserContext _context;

    public MockContextBrowserContentFetcher(IBrowserContext context)
    {
        _context = context;
    }

    protected override string BrowserNotInstalledMessage => "Test browser is unavailable.";

    protected override Task<IBrowserContext> CreateContextAsync(CancellationToken ct) =>
        Task.FromResult(_context);

    protected override Task DisposeBrowserResourcesAsync() => Task.CompletedTask;
}
