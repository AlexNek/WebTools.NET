using System.Text.RegularExpressions;

using Microsoft.Playwright;

using WebTools.NET.Abstractions;

namespace WebTools.NET.Browsing;

public sealed partial class PlaywrightSession : IBrowserInteraction
{
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
                                       WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 10000
                                   });

            var status = response?.Status ?? 0;
            if (status < 200 || status >= 300) return false;

            var finalUrl = page.Url;
            return !IsErrorPageUrl(finalUrl);
        }
        catch
        {
            return false;
        }
    }

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.ClickAsync(selector, new PageClickOptions { Timeout = 5000 });
        await page.WaitForLoadStateAsync(
            LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = 10000 });
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
            await _page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (_browser is not null)
            await _browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        _playwright?.Dispose();
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
        return WhitespaceRegex().Replace(TagRegex().Replace(body, " "), " ").Trim();
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
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 15000 });
    }

    private async Task<IPage> GetPageAsync(CancellationToken ct)
    {
        _playwright ??= await Playwright.CreateAsync();
        _browser ??=
            await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true });
        return _page ??= await _browser.NewPageAsync();
    }

    private static bool IsErrorPageUrl(string url) =>
        url.Contains("/notfound", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/error", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/404", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
