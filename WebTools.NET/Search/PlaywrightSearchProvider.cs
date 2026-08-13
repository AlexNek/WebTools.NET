using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Search;

public sealed class PlaywrightSearchProvider : IWebSearchProvider, IAsyncDisposable
{
    private const string ContextStealthScript =
        "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });" +
        "window.chrome = { runtime: {}, csi: function() {}, loadTimes: function() {} };";

    private readonly BrowserSearchEngine _engine;

    private readonly bool _headless;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IBrowser? _browser;

    private IBrowserContext? _context;

    private IPlaywright? _playwright;

    public PlaywrightSearchProvider(
        ILogger<PlaywrightSearchProvider>? logger = null,
        bool headless = true)
    {
        _headless = headless;
        _engine = new BrowserSearchEngine(GetPageAsync, logger, "Playwright");
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        _playwright?.Dispose();
        _initLock.Dispose();
    }

    public Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        return _engine.SearchAsync(query, maxResults, ct);
    }

    private async Task<IPage> GetPageAsync(CancellationToken ct)
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

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(
                           new BrowserTypeLaunchOptions
                               {
                                   Headless = _headless,
                                   Args =
                                       [
                                           "--disable-blink-features=AutomationControlled",
                                           "--disable-extensions",
                                           "--no-sandbox",
                                           "--disable-setuid-sandbox",
                                           "--disable-dev-shm-usage"
                                       ]
                               });

            var ua = BrowserSearchEngine.UserAgents[
                BrowserSearchEngine.Rng.Next(BrowserSearchEngine.UserAgents.Length)];
            _context = await _browser.NewContextAsync(
                           new BrowserNewContextOptions
                               {
                                   UserAgent = ua,
                                   Locale = "en-US",
                                   ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
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
