using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;

namespace WebTools.NET.Browsing;

public sealed class PlaywrightSession : IBrowserInteraction
{
    private const int ClickTimeoutMs = 5000;

    private const int NavigateTimeoutMs = 15000;

    private const int NetworkIdleTimeoutMs = 10000;

    private const int ReachabilityTimeoutMs = 10000;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IBrowser? _browser;

    private IPage? _page;

    private IPlaywright? _playwright;

    public async Task<bool> CheckReachabilityAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var page = await GetPageAsync(ct);
            var response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded,
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
        await page.ClickAsync(selector, new PageClickOptions { Timeout = ClickTimeoutMs });
        await page.WaitForLoadStateAsync(
            LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = NetworkIdleTimeoutMs });
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
            await _page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (_browser is not null)
            await _browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        _playwright?.Dispose();
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
            new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle, Timeout = NavigateTimeoutMs
                });
    }

    private async Task<IPage> GetPageAsync(CancellationToken ct)
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

            _playwright ??= await Playwright.CreateAsync();
            _browser ??=
                await _playwright.Chromium.LaunchAsync(
                    new BrowserTypeLaunchOptions { Headless = true });
            return _page ??= await _browser.NewPageAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }
}
