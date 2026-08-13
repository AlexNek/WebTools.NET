using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

public sealed class PlaywrightContentFetcher : IWebContentFetcher
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    private const string ChallengeWaitScript =
        "() => document.title !== 'Just a moment...' && " +
        "!document.body.textContent.includes('cf-browser-verification')";

    private const int ChallengeWaitMs = 30_000;

    private const int ContentLimit = 8000;

    private const int ErrorContentLimit = 3000;

    private const int FallbackNetworkIdleWaitMs = 10_000;

    private const int FallbackTimeoutMs = 90_000;

    private const int FetchGotoTimeoutMs = 20_000;

    private const int GotoTimeoutMs = 15_000;

    private const int NetworkIdleWaitMs = 5_000;

    private const string PlaywrightNotInstalledMessage =
        "Playwright browsers not installed. Run: playwright install (or .\\playwright.ps1 install)";

    private readonly bool _allowVisibleFallback;

    private readonly SemaphoreSlim _fallbackLock = new(1, 1);

    private readonly bool _headless;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IBrowser? _browser;

    private IBrowserContext? _context;

    private IPlaywright? _playwright;

    /// <param name="headless">Run the browser headless (default).</param>
    /// <param name="allowVisibleFallback">
    /// When true, a visible (non-headless) browser window may be opened as a last-resort
    /// fallback for pages stuck behind bot challenges. Off by default — popping a window
    /// is hostile to server/service contexts.
    /// </param>
    public PlaywrightContentFetcher(bool headless = true, bool allowVisibleFallback = false)
    {
        _headless = headless;
        _allowVisibleFallback = allowVisibleFallback;
    }

    public async Task<UrlCheckResult> CheckReachabilityAsync(
        string url,
        CancellationToken ct = default)
    {
        IPage? page = null;
        try
        {
            var context = await GetContextAsync(ct);
            page = await CreateStealthPageAsync(context);

            var response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded,
                                       Timeout = GotoTimeoutMs
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = await CreateStealthPageAsync(context);
                response = await BrowserHelpers.WarmupAndGotoAsync(page, url, GotoTimeoutMs, ct);

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            // After retry still blocked — try non-headless fallback for Cloudflare
            if (status is 403 or 429 && await IsBotChallengePageAsync(page))
            {
                if (!_allowVisibleFallback)
                {
                    return new UrlCheckResult(
                        false,
                        status,
                        "Blocked by bot protection",
                        FinalUrl: finalUrl,
                        ProtectionType: "Cloudflare");
                }

                return await NonHeadlessReachabilityFallbackAsync(url, ct);
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
        catch (PlaywrightException ex)
        {
            return new UrlCheckResult(
                false,
                null,
                BrowserHelpers.NormalizePlaywrightError(ex, PlaywrightNotInstalledMessage));
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
            await _context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        _playwright?.Dispose();
        _initLock.Dispose();
        _fallbackLock.Dispose();
    }

    public async Task<WebContent> FetchAsync(string url, CancellationToken ct = default)
    {
        IPage? page = null;
        try
        {
            var context = await GetContextAsync(ct);
            page = await CreateStealthPageAsync(context);

            var response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded,
                                       Timeout = FetchGotoTimeoutMs
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = await CreateStealthPageAsync(context);
                response = await BrowserHelpers.WarmupAndGotoAsync(page, url, GotoTimeoutMs, ct);

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            // If still Cloudflare-blocked after retry, try non-headless fallback
            if (status == 403 && await IsBotChallengePageAsync(page))
            {
                if (!_allowVisibleFallback)
                {
                    return new WebContent(false, "", "Blocked by bot protection", finalUrl);
                }

                return await NonHeadlessFetchFallbackAsync(url, ct);
            }

            // Give JS a moment to render, but don't wait for full NetworkIdle
            // (sites like openai.com never reach idle due to analytics/tracking)
            try
            {
                await page.WaitForLoadStateAsync(
                    LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = NetworkIdleWaitMs });
            }
            catch (TimeoutException)
            {
                // Fine — we already have DOMContentLoaded, proceed with what we have
            }

            finalUrl = page.Url;
            status = response?.Status ?? 0;
            var body = await page.TextContentAsync("body") ?? "";

            if (HtmlUtils.IsErrorPageUrl(finalUrl) && status >= 200 && status < 300)
            {
                return new WebContent(
                    false,
                    HtmlUtils.Truncate(HtmlUtils.StripHtml(body), ErrorContentLimit),
                    $"Redirected to error page ({finalUrl})",
                    finalUrl);
            }

            if (status < 200 || status >= 300)
            {
                return new WebContent(
                    false,
                    HtmlUtils.Truncate(HtmlUtils.StripHtml(body), ErrorContentLimit),
                    $"HTTP {status}",
                    finalUrl);
            }

            return new WebContent(
                true,
                HtmlUtils.Truncate(HtmlUtils.StripHtml(body), ContentLimit),
                null,
                finalUrl);
        }
        catch (TimeoutException)
        {
            return new WebContent(false, "", "Request timed out", url);
        }
        catch (PlaywrightException ex)
        {
            return new WebContent(
                false,
                "",
                BrowserHelpers.NormalizePlaywrightError(ex, PlaywrightNotInstalledMessage),
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

    private async Task<IPage> CreateStealthPageAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        try
        {
            await page.AddInitScriptAsync(StealthScripts.ForMode(_headless));
        }
        catch
        {
            // stealth injection failed — continue anyway
        }

        return page;
    }

    private async Task<IBrowserContext> GetContextAsync(CancellationToken ct)
    {
        if (_context is not null)
        {
            return _context;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_context is not null)
            {
                return _context;
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
                                           "--disable-infobars",
                                           "--no-sandbox",
                                           "--disable-setuid-sandbox",
                                           "--disable-dev-shm-usage",
                                           "--disable-renderer-backgrounding",
                                           "--disable-backgrounding-occluded-windows"
                                       ]
                               });
            _context = await _browser.NewContextAsync(
                           new BrowserNewContextOptions
                               {
                                   UserAgent = BrowserUserAgent,
                                   Locale = "en-US",
                                   TimezoneId = "America/New_York",
                                   ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                                   BypassCSP = true,
                                   JavaScriptEnabled = true
                               });
            return _context;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task<bool> IsBotChallengePageAsync(IPage page)
    {
        try
        {
            var title = await page.TitleAsync();

            // Title-based detection is the primary indicator.
            // Cloudflare challenge pages always set one of these titles.
            if (title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Attention Required", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Body-based detection only applies when the title is ambiguous
            // and the status code is 403/429. Legitimate pages behind Cloudflare
            // CDN often include CF-related strings in their body.
            return false;
        }
        catch
        {
            return false;
        }
    }

    private Task<WebContent> NonHeadlessFetchFallbackAsync(string url, CancellationToken ct)
    {
        return WithNonHeadlessBrowserAsync(
            url,
            ct,
            async (page, status, finalUrl, challenged) =>
            {
                // If Cloudflare challenge detected and unresolved, fail
                if (challenged)
                {
                    return new WebContent(false, "", "Blocked by bot protection", finalUrl);
                }

                try
                {
                    await page.WaitForLoadStateAsync(
                        LoadState.NetworkIdle,
                        new PageWaitForLoadStateOptions { Timeout = FallbackNetworkIdleWaitMs });
                }
                catch (TimeoutException)
                {
                    // proceed with what we have
                }

                finalUrl = page.Url;
                var body = await page.TextContentAsync("body") ?? "";

                if (status < 200 || status >= 300)
                {
                    return new WebContent(
                        false,
                        HtmlUtils.Truncate(HtmlUtils.StripHtml(body), ErrorContentLimit),
                        $"HTTP {status}",
                        finalUrl);
                }

                return new WebContent(
                    true,
                    HtmlUtils.Truncate(HtmlUtils.StripHtml(body), ContentLimit),
                    null,
                    finalUrl);
            });
    }

    private Task<UrlCheckResult> NonHeadlessReachabilityFallbackAsync(
        string url,
        CancellationToken ct)
    {
        return WithNonHeadlessBrowserAsync(
            url,
            ct,
            (page, status, finalUrl, challenged) =>
            {
                if (!challenged && status >= 200 && status < 400)
                {
                    return Task.FromResult(new UrlCheckResult(true, status, null, FinalUrl: finalUrl));
                }

                if (challenged)
                {
                    return Task.FromResult(
                        new UrlCheckResult(
                            false,
                            status,
                            "Blocked by bot protection",
                            FinalUrl: finalUrl,
                            ProtectionType: "Cloudflare"));
                }

                return Task.FromResult(
                    new UrlCheckResult(false, status, $"HTTP {status}", FinalUrl: finalUrl));
            });
    }

    /// <summary>
    /// Launches a visible browser, navigates to <paramref name="url"/> and waits for a
    /// possible bot challenge to resolve, then invokes <paramref name="actionAsync"/>
    /// with the page, the normalized status, the final URL and whether the page is
    /// still on a challenge page. Browser resources are always cleaned up afterwards.
    /// </summary>
    private async Task<T> WithNonHeadlessBrowserAsync<T>(
        string url,
        CancellationToken ct,
        Func<IPage, int, string, bool, Task<T>> actionAsync)
    {
        await _fallbackLock.WaitAsync(ct);
        try
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(
                              new BrowserTypeLaunchOptions
                                  {
                                      Headless = false,
                                      Args =
                                          [
                                              "--disable-blink-features=AutomationControlled",
                                              "--disable-extensions",
                                              "--no-sandbox"
                                          ]
                                  });

            var context = await browser.NewContextAsync(
                              new BrowserNewContextOptions
                                  {
                                      UserAgent = BrowserUserAgent,
                                      Locale = "en-US",
                                      TimezoneId = "America/New_York",
                                      ViewportSize =
                                          new ViewportSize { Width = 1920, Height = 1080 },
                                      BypassCSP = true,
                                      JavaScriptEnabled = true
                                  });

            var page = await context.NewPageAsync();
            try
            {
                // Non-headless: minimal stealth only — the visible browser window
                // already looks legitimate; heavy overrides trigger Cloudflare
                await page.AddInitScriptAsync(StealthScripts.Minimal);
            }
            catch
            {
                // stealth injection failed — continue anyway
            }

            try
            {
                var response = await page.GotoAsync(
                                   url,
                                   new PageGotoOptions
                                       {
                                           WaitUntil = WaitUntilState.DOMContentLoaded,
                                           Timeout = FallbackTimeoutMs
                                       });

                var status = response?.Status ?? 0;
                var finalUrl = page.Url;

                // Wait for a possible bot challenge to resolve (up to ChallengeWaitMs)
                var challenged = status == 403 || await IsBotChallengePageAsync(page);
                if (challenged)
                {
                    try
                    {
                        await page.WaitForFunctionAsync(
                            ChallengeWaitScript,
                            new PageWaitForFunctionOptions { Timeout = ChallengeWaitMs });
                        finalUrl = page.Url;
                    }
                    catch (TimeoutException)
                    {
                        // challenge didn't resolve
                    }
                }

                var stillChallenged = await IsBotChallengePageAsync(page);
                if (challenged && !stillChallenged)
                {
                    // The original response was the challenge interstitial —
                    // the page actually loaded once the challenge cleared.
                    status = BrowserHelpers.NormalizeStatusAfterChallenge(status, true);
                }

                return await actionAsync(page, status, finalUrl, stillChallenged);
            }
            finally
            {
                await page.CloseAsync().ConfigureAwait(false);
                await context.CloseAsync();
                await browser.CloseAsync();
                playwright.Dispose();
            }
        }
        finally
        {
            _fallbackLock.Release();
        }
    }
}
