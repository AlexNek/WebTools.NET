using System.Text.RegularExpressions;

using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

public sealed partial class PlaywrightContentFetcher : IWebContentFetcher, IAsyncDisposable
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    private const int FallbackTimeoutMs = 90_000;

    private static readonly string[] StealthScript =
        [
            // Overwrite the `navigator.webdriver` property to undefined
            "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });",

            // Remove Chrome automation extensions
            "window.chrome = { runtime: {}, csi: function() {}, loadTimes: function() {} };",

            // Override permissions
            "const originalQuery = window.navigator.permissions.query;",
            "window.navigator.permissions.query = (parameters) => (",
            "    parameters.name === 'notifications' ?",
            "        Promise.resolve({ state: Notification.permission }) :",
            "        originalQuery(parameters)",
            ");",

            // WebGL vendor/renderer spoofing (common bot detection vector)
            "const getParameter = WebGLRenderingContext.prototype.getParameter;",
            "WebGLRenderingContext.prototype.getParameter = function(param) {",
            "    if (param === 37445) return 'Intel Inc.';",
            "    if (param === 37446) return 'Intel Iris Xe Graphics';",
            "    return getParameter.call(this, param);",
            "};",

            // Plugins spoofing - realistic plugin list
            "Object.defineProperty(navigator, 'plugins', {",
            "    get: () => {",
            "        const plugins = [];",
            "        const names = ['PDF Viewer', 'Chrome PDF Viewer', 'Chromium PDF Viewer',",
            "                       'Microsoft Edge PDF Viewer', 'WebKit built-in PDF',",
            "                       'Widevine Content Decryption Module', 'Widevine Content Decryption Module'];",
            "        for (let i = 0; i < names.length; i++) {",
            "            plugins.push({",
            "                name: names[i],",
            "                filename: names[i].replace(/ /g, '_') + '.plugin'",
            "            });",
            "        }",
            "        return plugins;",
            "    }",
            "});",

            // Languages and hardware concurrency
            "Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });",
            "Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });",
            "Object.defineProperty(navigator, 'deviceMemory', { get: () => 8 });",
            "Object.defineProperty(navigator, 'maxTouchPoints', { get: () => 0 });",

            // Screen properties
            "Object.defineProperty(screen, 'colorDepth', { get: () => 24 });",
            "Object.defineProperty(screen, 'pixelDepth', { get: () => 24 });",

            // Override toString/functions to avoid detection
            "const originalToString = Function.prototype.toString;",
            "Function.prototype.toString = function() {",
            "    if (this === navigator.permissions.query) {",
            "        return 'function query() { [native code] }';",
            "    }",
            "    return originalToString.call(this);",
            "};"
        ];

    private readonly SemaphoreSlim _fallbackLock = new(1, 1);

    private readonly bool _headless;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IBrowser? _browser;

    private IBrowserContext? _context;

    private IPlaywright? _playwright;

    public PlaywrightContentFetcher(bool headless = true)
    {
        _headless = headless;
    }

    public async Task<UrlCheckResult> CheckReachabilityAsync(
        string url,
        CancellationToken ct = default)
    {
        IPage? page = null;
        try
        {
            var context = await GetContextAsync(ct);
            page = await context.NewPageAsync();
            await ApplyStealthForModeAsync(page);

            var response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = null;

                page = await context.NewPageAsync();
                await ApplyStealthForModeAsync(page);

                try
                {
                    await page.GotoAsync(
                        "https://www.google.com",
                        new PageGotoOptions
                            {
                                WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 8000
                            });
                    await Task.Delay(300, ct);
                }
                catch
                {
                    // warmup failed — proceed anyway
                }

                response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000
                                   });

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            // After retry still blocked — try non-headless fallback for Cloudflare
            if (status is 403 or 429 && await IsBotChallengePageAsync(page))
            {
                return await NonHeadlessReachabilityFallbackAsync(url, ct);
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
        catch (PlaywrightException ex)
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
            page = await context.NewPageAsync();
            await ApplyStealthForModeAsync(page);

            var response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20000
                                   });

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;

            if (status == 403)
            {
                await page.CloseAsync().ConfigureAwait(false);
                page = null;

                page = await context.NewPageAsync();
                await ApplyStealthForModeAsync(page);

                try
                {
                    await page.GotoAsync(
                        "https://www.google.com",
                        new PageGotoOptions
                            {
                                WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 8000
                            });
                    await Task.Delay(300, ct);
                }
                catch
                {
                    // warmup failed — proceed anyway
                }

                response = await page.GotoAsync(
                               url,
                               new PageGotoOptions
                                   {
                                       WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000
                                   });

                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            // If still Cloudflare-blocked after retry, try non-headless fallback
            if (status == 403 && await IsBotChallengePageAsync(page))
            {
                return await NonHeadlessFetchFallbackAsync(url, ct);
            }

            // Give JS a moment to render, but don't wait for full NetworkIdle
            // (sites like openai.com never reach idle due to analytics/tracking)
            try
            {
                await page.WaitForLoadStateAsync(
                    LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                // Fine — we already have DOMContentLoaded, proceed with what we have
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
        catch (PlaywrightException ex)
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

    private static async Task ApplyMinimalStealthAsync(IPage page)
    {
        try
        {
            // Minimal stealth: only override the most commonly-checked property.
            // Heavy stealth scripts (WebGL/plugin/spoofing) can paradoxically
            // increase bot detection likelihood with advanced scanners.
            await page.AddInitScriptAsync(
                "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
        }
        catch
        {
            // stealth injection failed — continue anyway
        }
    }

    private static async Task ApplyStealthAsync(IPage page)
    {
        try
        {
            var script = string.Join("\n", StealthScript);
            await page.AddInitScriptAsync(script);
        }
        catch
        {
            // stealth injection failed — continue anyway
        }
    }

    private async Task ApplyStealthForModeAsync(IPage page)
    {
        if (_headless)
        {
            // Headless: use full stealth (more overrides needed)
            await ApplyStealthForModeAsync(page);
        }
        else
        {
            // Non-headless: minimal stealth only — the visible browser window
            // already looks legitimate; heavy overrides trigger Cloudflare
            await ApplyMinimalStealthAsync(page);
        }
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

    private static bool IsErrorPageUrl(string url) =>
        url.Contains("/notfound", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/error", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/404", StringComparison.OrdinalIgnoreCase);

    private async Task<WebContent> NonHeadlessFetchFallbackAsync(string url, CancellationToken ct)
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
            await ApplyMinimalStealthAsync(page);
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

                // If Cloudflare challenge detected, wait for it to resolve
                if (status == 403 || await IsBotChallengePageAsync(page))
                {
                    try
                    {
                        await page.WaitForFunctionAsync(
                            "() => document.title !== 'Just a moment...' && " +
                            "!document.body.textContent.includes('cf-browser-verification')",
                            new PageWaitForFunctionOptions { Timeout = 30_000 });
                        status = response?.Status ?? 0;
                        finalUrl = page.Url;
                    }
                    catch (TimeoutException)
                    {
                        // challenge didn't resolve
                    }
                }

                // If still challenged, fail
                if (await IsBotChallengePageAsync(page))
                {
                    return new WebContent(false, "", "Blocked by bot protection", finalUrl);
                }

                try
                {
                    await page.WaitForLoadStateAsync(
                        LoadState.NetworkIdle,
                        new PageWaitForLoadStateOptions { Timeout = 10_000 });
                }
                catch (TimeoutException)
                {
                    // proceed with what we have
                }

                finalUrl = page.Url;
                status = response?.Status ?? 0;
                var body = await page.TextContentAsync("body") ?? "";

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

    private async Task<UrlCheckResult> NonHeadlessReachabilityFallbackAsync(
        string url,
        CancellationToken ct)
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
            await ApplyMinimalStealthAsync(page);
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

                // Wait for Cloudflare challenge to resolve (up to 30s)
                if (status == 403 || await IsBotChallengePageAsync(page))
                {
                    try
                    {
                        await page.WaitForFunctionAsync(
                            "() => document.title !== 'Just a moment...' && " +
                            "!document.body.textContent.includes('cf-browser-verification')",
                            new PageWaitForFunctionOptions { Timeout = 30_000 });
                        status = response?.Status ?? 0;
                        finalUrl = page.Url;
                    }
                    catch (TimeoutException)
                    {
                        // Challenge didn't resolve
                    }
                }

                if (status >= 200 && status < 400 && !await IsBotChallengePageAsync(page))
                {
                    return new UrlCheckResult(true, status, null, FinalUrl: finalUrl);
                }

                return new UrlCheckResult(
                    false,
                    status,
                    "Blocked by bot protection",
                    FinalUrl: finalUrl,
                    ProtectionType: "Cloudflare");
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

    private static string NormalizePlaywrightError(PlaywrightException ex)
    {
        if (ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal))
        {
            return
                "Playwright browsers not installed. Run: playwright install (or .\\playwright.ps1 install)";
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
