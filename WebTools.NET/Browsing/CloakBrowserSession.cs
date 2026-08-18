using CloakBrowser;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;

namespace WebTools.NET.Browsing;

public sealed class CloakBrowserSession : IBrowserInteraction
{
    private const int ClickTimeoutMs = 5000;

    private const int NavigateTimeoutMs = 15000;

    private const int NetworkIdleTimeoutMs = 10000;

    private const int ReachabilityTimeoutMs = 10000;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private Microsoft.Playwright.IBrowser? _browser;

    private CloakBrowserHandle? _handle;

    private Microsoft.Playwright.IPage? _page;

    public async Task<bool> CheckReachabilityAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var page = await GetPageAsync(ct);
            var response = await page.GotoAsync(
                               url,
                               new Microsoft.Playwright.PageGotoOptions
                                   {
                                       WaitUntil =
                                           Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                       Timeout = ReachabilityTimeoutMs
                                   });

            var status = response?.Status ?? 0;
            if (HttpStatusHelper.IsNotSuccess(status)) return false;

            var finalUrl = page.Url;
            return !HtmlUtils.IsErrorPageUrl(finalUrl);
        }
        catch
        {
            return false;
        }
    }

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.ClickAsync(
            selector,
            new Microsoft.Playwright.PageClickOptions { Timeout = ClickTimeoutMs });
        await page.WaitForLoadStateAsync(
            Microsoft.Playwright.LoadState.NetworkIdle,
            new Microsoft.Playwright.PageWaitForLoadStateOptions
                {
                    Timeout = NetworkIdleTimeoutMs
                });
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
            try
            {
                await _page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }

        if (_handle is not null)
            try
            {
                await _handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }

        _page = null;
        _browser = null;
        _handle = null;
        _initLock.Dispose();
    }

    public async Task FillAsync(string selector, string value, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.FillAsync(selector, value);
    }

    public async Task<string> GetContentAsync(CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        var body = await page.TextContentAsync("body") ?? "";
        return HtmlUtils.StripHtml(body);
    }

    public async Task<string> GetCurrentUrlAsync(CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        return page.Url;
    }

    public async Task<string> GetHtmlAsync(CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        return await page.ContentAsync();
    }

    public async Task NavigateAsync(string url, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.GotoAsync(
            url,
            new Microsoft.Playwright.PageGotoOptions
                {
                    WaitUntil = Microsoft.Playwright.WaitUntilState.NetworkIdle,
                    Timeout = NavigateTimeoutMs
                });
    }

    private async Task<Microsoft.Playwright.IPage> GetPageAsync(CancellationToken ct)
    {
        if (_page is not null)
        {
            return _page;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_page is not null)
            {
                return _page;
            }

            _handle ??= await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = true, });

            _browser = _handle.RawBrowser;
            return _page ??= await _browser.NewPageAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }
}
