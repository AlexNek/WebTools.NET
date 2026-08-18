using CloakBrowser;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

public sealed class CloakBrowserContentFetcher : IWebContentFetcher
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    private const string CloakNotInstalledMessage =
        "CloakBrowser binary not found. First launch should auto-download, or run: dotnet cloakbrowser install";

    private const int ErrorContentLimit = 3000;

    private const int FetchGotoTimeoutMs = 20_000;

    private const int GotoTimeoutMs = 15_000;

    private const int NetworkIdleWaitMs = 5_000;

    private readonly bool _headless;

    private readonly SemaphoreSlim _launchLock = new(1, 1);

    private Microsoft.Playwright.IBrowser? _browser;

    private Microsoft.Playwright.IBrowserContext? _context;

    private CloakBrowserHandle? _handle;

    public CloakBrowserContentFetcher(bool headless = true)
    {
        _headless = headless;
    }

    public async Task<UrlCheckResult> CheckReachabilityAsync(
        string url,
        CancellationToken ct = default)
    {
        Microsoft.Playwright.IPage? page = null;
        try
        {
            var context = await GetContextAsync(ct);
            page = await context.NewPageAsync();

            var response = await page.GotoAsync(
                               url,
                               new Microsoft.Playwright.PageGotoOptions
                                   {
                                       WaitUntil =
                                           Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                       Timeout = GotoTimeoutMs
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = await context.NewPageAsync();
                response = await BrowserHelpers.WarmupAndGotoAsync(page, url, GotoTimeoutMs, ct);

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            if (status >= 200 && status < 300 && HtmlUtils.IsErrorPageUrl(finalUrl))
            {
                return new UrlCheckResult(
                    false,
                    status,
                    $"Redirected to error page ({finalUrl})",
                    FinalUrl: finalUrl);
            }

            if (status >= 200 && status < 400)
            {
                return new UrlCheckResult(true, status, null, FinalUrl: finalUrl);
            }

            return new UrlCheckResult(false, status, $"HTTP {status}", FinalUrl: finalUrl);
        }
        catch (TimeoutException)
        {
            return new UrlCheckResult(false, null, "Timed out");
        }
        catch (Microsoft.Playwright.PlaywrightException ex)
        {
            return new UrlCheckResult(
                false,
                null,
                BrowserHelpers.NormalizePlaywrightError(ex, CloakNotInstalledMessage));
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }
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
        _launchLock.Dispose();
    }

    public async Task<WebContent> FetchAsync(string url, int? maxContentLength = null, CancellationToken ct = default)
    {
        if (maxContentLength is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxContentLength), maxContentLength, "Value must be a positive integer or null.");
        }

        Microsoft.Playwright.IPage? page = null;
        try
        {
            var context = await GetContextAsync(ct);
            page = await context.NewPageAsync();

            var response = await page.GotoAsync(
                               url,
                               new Microsoft.Playwright.PageGotoOptions
                                   {
                                       WaitUntil =
                                           Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                       Timeout = FetchGotoTimeoutMs
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = await context.NewPageAsync();
                response = await BrowserHelpers.WarmupAndGotoAsync(page, url, GotoTimeoutMs, ct);

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            try
            {
                await page.WaitForLoadStateAsync(
                    Microsoft.Playwright.LoadState.NetworkIdle,
                    new Microsoft.Playwright.PageWaitForLoadStateOptions
                        {
                            Timeout = NetworkIdleWaitMs
                        });
            }
            catch (TimeoutException)
            {
            }

            finalUrl = page.Url;
            status = response?.Status ?? 0;
            var body = await page.TextContentAsync("body") ?? "";
            var stripped = HtmlUtils.StripHtml(body);

            if (HtmlUtils.IsErrorPageUrl(finalUrl) && status >= 200 && status < 300)
            {
                return new WebContent(
                    false,
                    HtmlUtils.Truncate(stripped, ErrorContentLimit),
                    $"Redirected to error page ({finalUrl})",
                    finalUrl);
            }

            if (status < 200 || status >= 300)
            {
                return new WebContent(
                    false,
                    HtmlUtils.Truncate(stripped, ErrorContentLimit),
                    $"HTTP {status}",
                    finalUrl);
            }

            return new WebContent(
                true,
                HtmlUtils.TruncateIfNeeded(stripped, maxContentLength),
                null,
                finalUrl);
        }
        catch (TimeoutException)
        {
            return new WebContent(false, "", "Request timed out", url);
        }
        catch (Microsoft.Playwright.PlaywrightException ex)
        {
            return new WebContent(
                false,
                "",
                BrowserHelpers.NormalizePlaywrightError(ex, CloakNotInstalledMessage),
                url);
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<Microsoft.Playwright.IBrowserContext> GetContextAsync(CancellationToken ct)
    {
        if (_context is not null)
        {
            return _context;
        }

        await _launchLock.WaitAsync(ct);
        try
        {
            if (_context is not null)
            {
                return _context;
            }

            _handle = await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = _headless, });

            _browser = _handle.RawBrowser;
            _context = await _browser.NewContextAsync(
                           new Microsoft.Playwright.BrowserNewContextOptions
                               {
                                   UserAgent = BrowserUserAgent,
                                   Locale = "en-US",
                                   TimezoneId = "America/New_York",
                                   ViewportSize =
                                       new Microsoft.Playwright.ViewportSize
                                           {
                                               Width = 1920, Height = 1080
                                           },
                                   BypassCSP = true,
                                   JavaScriptEnabled = true
                               });

            return _context;
        }
        finally
        {
            _launchLock.Release();
        }
    }
}
