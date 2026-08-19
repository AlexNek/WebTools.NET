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

    private const int ScrollNetworkIdleMs = 3000;

    private const int WaitForSelectorDefaultMs = 5000;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly string? _storageStatePath;

    private IBrowser? _browser;

    private IPage? _page;

    private IPlaywright? _playwright;

    public PlaywrightSession(string? storageStatePath = null)
    {
        _storageStatePath = storageStatePath;
    }

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

    public async Task<string> GetTitleAsync(CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        return await page.TitleAsync();
    }

    public async Task GoBackAsync(CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.GoBackAsync(new PageGoBackOptions { Timeout = NavigateTimeoutMs });
    }

    public async Task LoadStorageStateAsync(string path, CancellationToken ct = default)
    {
        // Storage state is applied during context creation.
        // If a page already exists we cannot re-apply, so this is a no-op after init.
        // The path is stored and used in GetPageAsync on first initialization.
        await Task.CompletedTask;
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

    public async Task SaveStorageStateAsync(string path, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        var context = page.Context;
        await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = path });
    }

    public async Task<string> ScreenshotAsync(CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = false });
        return Convert.ToBase64String(bytes);
    }

    public async Task ScrollAsync(int deltaY, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.Mouse.WheelAsync(0, deltaY);

        // Wait briefly for lazy-loaded content to appear
        try
        {
            await page.WaitForLoadStateAsync(
                LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = ScrollNetworkIdleMs });
        }
        catch (TimeoutException)
        {
            // Fine — not all pages trigger network requests on scroll
        }
    }

    public async Task SelectOptionAsync(string selector, string value, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.SelectOptionAsync(selector, new SelectOptionValue { Label = value });
    }

    public async Task SubmitFormAsync(string selector, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        await page.EvalOnSelectorAsync(
            selector,
            "el => { const form = el.closest('form'); if (form) form.submit(); }");

        try
        {
            await page.WaitForLoadStateAsync(
                LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = NetworkIdleTimeoutMs });
        }
        catch (TimeoutException)
        {
            // Proceed — some forms use AJAX without full navigation
        }
    }

    public async Task WaitForSelectorAsync(string selector, int timeoutMs, CancellationToken ct = default)
    {
        var page = await GetPageAsync(ct);
        var timeout = timeoutMs > 0 ? timeoutMs : WaitForSelectorDefaultMs;
        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = timeout });
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

            var contextOptions = new BrowserNewContextOptions();
            if (_storageStatePath is not null && File.Exists(_storageStatePath))
            {
                contextOptions.StorageStatePath = _storageStatePath;
            }

            var context = await _browser.NewContextAsync(contextOptions);
            return _page ??= await context.NewPageAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }
}
