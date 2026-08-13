using System.Text.RegularExpressions;

using CloakBrowser;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

public sealed partial class CloakBrowserContentFetcher : IWebContentFetcher, IAsyncDisposable
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    private const int FallbackTimeoutMs = 90_000;

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
                                       Timeout = 15000
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = null;

                page = await context.NewPageAsync();
                try
                {
                    await page.GotoAsync(
                        "https://www.google.com",
                        new Microsoft.Playwright.PageGotoOptions
                            {
                                WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                Timeout = 8000
                            });
                    await Task.Delay(300, ct);
                }
                catch
                {
                }

                response = await page.GotoAsync(
                               url,
                               new Microsoft.Playwright.PageGotoOptions
                                   {
                                       WaitUntil =
                                           Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                       Timeout = 15000
                                   });

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            if (status >= 200 && status < 300 && IsErrorPageUrl(finalUrl))
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
            return new UrlCheckResult(false, null, NormalizePlaywrightError(ex));
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

    public async Task<WebContent> FetchAsync(string url, CancellationToken ct = default)
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
                                       Timeout = 20000
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = null;

                page = await context.NewPageAsync();
                try
                {
                    await page.GotoAsync(
                        "https://www.google.com",
                        new Microsoft.Playwright.PageGotoOptions
                            {
                                WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                Timeout = 8000
                            });
                    await Task.Delay(300, ct);
                }
                catch
                {
                }

                response = await page.GotoAsync(
                               url,
                               new Microsoft.Playwright.PageGotoOptions
                                   {
                                       WaitUntil =
                                           Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
                                       Timeout = 15000
                                   });

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            try
            {
                await page.WaitForLoadStateAsync(
                    Microsoft.Playwright.LoadState.NetworkIdle,
                    new Microsoft.Playwright.PageWaitForLoadStateOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
            }

            finalUrl = page.Url;
            status = response?.Status ?? 0;
            var body = await page.TextContentAsync("body") ?? "";

            if (IsErrorPageUrl(finalUrl) && status >= 200 && status < 300)
            {
                var text = StripHtml(body);
                return new WebContent(
                    false,
                    Truncate(text, 3000),
                    $"Redirected to error page ({finalUrl})",
                    finalUrl);
            }

            if (status < 200 || status >= 300)
            {
                var text = StripHtml(body);
                return new WebContent(
                    false,
                    Truncate(text, 3000),
                    $"HTTP {status}",
                    finalUrl);
            }

            var content = StripHtml(body);
            return new WebContent(true, Truncate(content, 8000), null, finalUrl);
        }
        catch (TimeoutException)
        {
            return new WebContent(false, "", "Request timed out", url);
        }
        catch (Microsoft.Playwright.PlaywrightException ex)
        {
            return new WebContent(false, "", NormalizePlaywrightError(ex), url);
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

    private static bool IsErrorPageUrl(string url) =>
        url.Contains("/notfound", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/error", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/404", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePlaywrightError(Microsoft.Playwright.PlaywrightException ex)
    {
        if (ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal))
        {
            return
                "CloakBrowser binary not found. First launch should auto-download, or run: dotnet cloakbrowser install";
        }

        return ex.Message;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        return WhitespaceRegex().Replace(TagRegex().Replace(html, " "), " ").Trim();
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "\n... [truncated]";

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
