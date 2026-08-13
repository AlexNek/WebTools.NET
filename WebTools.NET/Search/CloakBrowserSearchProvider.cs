using CloakBrowser;

using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Search;

public sealed class CloakBrowserSearchProvider : IWebSearchProvider, IAsyncDisposable
{
    private const string ContextStealthScript =
        "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });" +
        "window.chrome = { runtime: {}, csi: function() {}, loadTimes: function() {} };";

    private readonly BrowserSearchEngine _engine;

    private readonly bool _headless;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private Microsoft.Playwright.IBrowser? _browser;

    private Microsoft.Playwright.IBrowserContext? _context;

    private CloakBrowserHandle? _handle;

    public CloakBrowserSearchProvider(
        ILogger<CloakBrowserSearchProvider>? logger = null,
        bool headless = true)
    {
        _headless = headless;
        _engine = new BrowserSearchEngine(GetPageAsync, logger, "CloakBrowser");
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            try
            {
                await _context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (_handle is not null)
        {
            try
            {
                await _handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _context = null;
        _browser = null;
        _handle = null;
        _initLock.Dispose();
    }

    public Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        return _engine.SearchAsync(query, maxResults, ct);
    }

    private async Task<Microsoft.Playwright.IPage> GetPageAsync(CancellationToken ct)
    {
        if (_context is not null)
        {
            return await _context.NewPageAsync();
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_context is not null)
            {
                return await _context.NewPageAsync();
            }

            _handle = await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = _headless, });

            _browser = _handle.RawBrowser;

            var ua = BrowserSearchEngine.UserAgents[
                BrowserSearchEngine.Rng.Next(BrowserSearchEngine.UserAgents.Length)];
            _context = await _browser.NewContextAsync(
                           new Microsoft.Playwright.BrowserNewContextOptions
                               {
                                   UserAgent = ua,
                                   Locale = "en-US",
                                   ViewportSize =
                                       new Microsoft.Playwright.ViewportSize
                                           {
                                               Width = 1920, Height = 1080
                                           }
                               });

            // Apply stealth on the context so every page created from it inherits it
            await _context.AddInitScriptAsync(ContextStealthScript);

            return await _context.NewPageAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }
}
