using CloakBrowser;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

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

    private readonly SemaphoreSlim _fallbackLock = new(1, 1);

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IBrowser? _browser;

    private IBrowserContext? _context;

    private CloakBrowserHandle? _handle;

    private int _disposed;

    public CloakBrowserSearchProvider(
        ILogger<CloakBrowserSearchProvider>? logger = null,
        bool headless = true)
    {
        _headless = headless;
        _engine = new BrowserSearchEngine(
            GetPageAsync,
            logger,
            "CloakBrowser",
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
            await CloseContextQuietlyAsync(context).ConfigureAwait(false);

            var handle = Interlocked.Exchange(ref _handle, null);
            await DisposeHandleQuietlyAsync(handle).ConfigureAwait(false);
            Interlocked.Exchange(ref _browser, null);
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

            _handle = await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = _headless });
            _browser = _handle.RawBrowser;

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

        CloakBrowserHandle? handle = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;
        IPage? page = null;
        try
        {
            ThrowIfDisposed();
            handle = await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = false });
            browser = handle.RawBrowser;
            context = await browser.NewContextAsync(CreateContextOptions());
            await context.AddInitScriptAsync(ContextStealthScript);
            page = await context.NewPageAsync();

            return new SearchPageLease(
                page,
                () => DisposeFallbackResourcesAsync(context, handle),
                ReleaseFallbackLockAsync);
        }
        catch
        {
            await ClosePageQuietlyAsync(page).ConfigureAwait(false);
            await CloseContextQuietlyAsync(context).ConfigureAwait(false);
            await DisposeHandleQuietlyAsync(handle).ConfigureAwait(false);
            _fallbackLock.Release();
            throw;
        }
    }

    private ValueTask ReleaseFallbackLockAsync()
    {
        _fallbackLock.Release();
        return ValueTask.CompletedTask;
    }

    private static BrowserNewContextOptions CreateContextOptions() => new()
    {
        UserAgent = BrowserSearchEngine.UserAgents[
            BrowserSearchEngine.Rng.Next(BrowserSearchEngine.UserAgents.Length)],
        Locale = "en-US",
        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
    };

    private static async ValueTask DisposeFallbackResourcesAsync(
        IBrowserContext? context,
        CloakBrowserHandle? handle)
    {
        await CloseContextQuietlyAsync(context).ConfigureAwait(false);
        await DisposeHandleQuietlyAsync(handle).ConfigureAwait(false);
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

    private static async ValueTask DisposeHandleQuietlyAsync(CloakBrowserHandle? handle)
    {
        if (handle is null)
        {
            return;
        }

        try
        {
            await handle.DisposeAsync().ConfigureAwait(false);
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
