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

    private readonly SemaphoreSlim _fallbackLock = new(1, 1);

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IBrowser? _browser;

    private IBrowserContext? _context;

    private IPlaywright? _playwright;

    private int _disposed;

    public PlaywrightSearchProvider(
        ILogger<PlaywrightSearchProvider>? logger = null,
        bool headless = true)
    {
        _headless = headless;
        _engine = new BrowserSearchEngine(
            GetPageAsync,
            logger,
            "Playwright",
            headless ? CreateFallbackPageLeaseAsync : null);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _fallbackLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var context = Interlocked.Exchange(ref _context, null);
            if (context is not null)
            {
                await CloseContextQuietlyAsync(context).ConfigureAwait(false);
            }

            var browser = Interlocked.Exchange(ref _browser, null);
            await CloseBrowserQuietlyAsync(browser).ConfigureAwait(false);

            var playwright = Interlocked.Exchange(ref _playwright, null);
            playwright?.Dispose();
        }
        finally
        {
            _fallbackLock.Release();
            _fallbackLock.Dispose();
            _initLock.Dispose();
        }
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
        ThrowIfDisposed();

        if (_context is not null)
        {
            return await _context.NewPageAsync();
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_context is not null)
            {
                return await _context.NewPageAsync();
            }

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(CreateLaunchOptions(_headless));

            _context = await _browser.NewContextAsync(CreateContextOptions());
            await _context.AddInitScriptAsync(ContextStealthScript);

            return await _context.NewPageAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<ISearchPageLease> CreateFallbackPageLeaseAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        await _fallbackLock.WaitAsync(ct).ConfigureAwait(false);

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;
        IPage? page = null;
        try
        {
            ThrowIfDisposed();
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(CreateLaunchOptions(headless: false));
            context = await browser.NewContextAsync(CreateContextOptions());
            await context.AddInitScriptAsync(ContextStealthScript);
            page = await context.NewPageAsync();

            return new SearchPageLease(
                page,
                () => DisposeFallbackResourcesAsync(context, browser, playwright),
                ReleaseFallbackLockAsync);
        }
        catch
        {
            await ClosePageQuietlyAsync(page).ConfigureAwait(false);
            await DisposeFallbackResourcesAsync(context, browser, playwright).ConfigureAwait(false);
            _fallbackLock.Release();
            throw;
        }
    }

    private ValueTask ReleaseFallbackLockAsync()
    {
        _fallbackLock.Release();
        return ValueTask.CompletedTask;
    }

    private static BrowserTypeLaunchOptions CreateLaunchOptions(bool headless) => new()
    {
        Headless = headless,
        Args =
        [
            "--disable-blink-features=AutomationControlled",
            "--disable-extensions",
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-dev-shm-usage"
        ]
    };

    private static BrowserNewContextOptions CreateContextOptions() => new()
    {
        UserAgent = BrowserSearchEngine.UserAgents[
            BrowserSearchEngine.Rng.Next(BrowserSearchEngine.UserAgents.Length)],
        Locale = "en-US",
        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
    };

    private static async ValueTask DisposeFallbackResourcesAsync(
        IBrowserContext? context,
        IBrowser? browser,
        IPlaywright? playwright)
    {
        await CloseContextQuietlyAsync(context).ConfigureAwait(false);
        await CloseBrowserQuietlyAsync(browser).ConfigureAwait(false);
        try
        {
            playwright?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private static async ValueTask ClosePageQuietlyAsync(IPage? page)
    {
        if (page is null)
        {
            return;
        }

        try
        {
            await page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static async ValueTask CloseContextQuietlyAsync(IBrowserContext? context)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            await context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static async ValueTask CloseBrowserQuietlyAsync(IBrowser? browser)
    {
        if (browser is null)
        {
            return;
        }

        try
        {
            await browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
