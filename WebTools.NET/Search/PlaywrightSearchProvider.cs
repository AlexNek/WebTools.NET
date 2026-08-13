using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Search;

public sealed partial class PlaywrightSearchProvider : IWebSearchProvider, IAsyncDisposable
{
    private static readonly Random Rng = new();

    private static readonly string[] UserAgents =
        [
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
        ];

    private readonly bool _headless;

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly ILogger<PlaywrightSearchProvider>? _logger;

    private IBrowser? _browser;

    private IBrowserContext? _context;

    private IPlaywright? _playwright;

    public PlaywrightSearchProvider(
        ILogger<PlaywrightSearchProvider>? logger = null,
        bool headless = true)
    {
        _logger = logger;
        _headless = headless;
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
    }

    public async Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        try
        {
            await Task.Delay(Rng.Next(500, 2000), ct);

            await using var page = await GetPageAsync(ct);
            var result = await SearchBingAsync(page, query, maxResults);
            if (result.Success && result.Results.Count > 0)
                return result;

            var bingError = result.ErrorMessage ?? "No Bing results";

            await Task.Delay(3000, ct);
            var ddgResult = await SearchDdgAsync(page, query, maxResults);
            if (ddgResult.Success && ddgResult.Results.Count > 0)
                return ddgResult;

            var ddgError = ddgResult.ErrorMessage ?? "no results";
            return new SearchResult(false, [], $"{bingError}; DuckDuckGo fallback: {ddgError}");
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogError(ex, "Playwright error for query '{Query}'", query);
            return new SearchResult(false, [], $"Playwright error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return new SearchResult(false, [], "Search timed out");
        }
    }

    private static string ExtractBingTrackingUrl(string rawUrl)
    {
        if (!rawUrl.StartsWith("https://www.bing.com/ck/", StringComparison.OrdinalIgnoreCase))
            return rawUrl;

        var queryStart = rawUrl.IndexOf('?');
        if (queryStart < 0) return rawUrl;

        var query = rawUrl[(queryStart + 1)..];
        foreach (var part in query.Split('&'))
        {
            if (!part.StartsWith("u=", StringComparison.Ordinal)) continue;
            try
            {
                var encoded = Uri.UnescapeDataString(part[2..]);

                var b64Start = encoded.IndexOf("aHR0", StringComparison.Ordinal);
                if (b64Start < 0)
                {
                    var prefixLen = 0;
                    for (var i = 0; i < encoded.Length && i < 4; i++)
                    {
                        if (encoded[i..].StartsWith("aHR0", StringComparison.Ordinal))
                        {
                            prefixLen = i;
                            break;
                        }
                    }

                    b64Start = prefixLen;
                }

                var b64Payload = encoded[b64Start..];
                var remainder = b64Payload.Length % 4;
                var padded = remainder switch
                    {
                        2 => b64Payload + "==",
                        3 => b64Payload + "=",
                        _ => b64Payload
                    };

                var bytes = Convert.FromBase64String(padded);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);

                if (decoded.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    decoded.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return decoded;
                }

                return rawUrl;
            }
            catch
            {
                return rawUrl;
            }
        }

        return rawUrl;
    }

    private async Task<IPage> GetPageAsync(CancellationToken ct)
    {
        if (_context is not null)
        {
            return await _context.NewPageAsync();
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_context is not null)
            {
                return await _context.NewPageAsync();
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
                                           "--no-sandbox",
                                           "--disable-setuid-sandbox",
                                           "--disable-dev-shm-usage"
                                       ]
                               });

            var ua = UserAgents[Rng.Next(UserAgents.Length)];
            _context = await _browser.NewContextAsync(
                           new BrowserNewContextOptions
                               {
                                   UserAgent = ua,
                                   Locale = "en-US",
                                   ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                               });

            // Apply stealth script on pages from this context
            var page = await _context.NewPageAsync();
            await page.AddInitScriptAsync(
                "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });" +
                "window.chrome = { runtime: {}, csi: function() {}, loadTimes: function() {} };");
            return page;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<SearchResult> SearchBingAsync(IPage page, string query, int maxResults)
    {
        await page.GotoAsync(
            "https://www.bing.com/",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000 });

        try
        {
            await page.Locator("#sb_form_q")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        }
        catch
        {
            return new SearchResult(false, [], "Bing blocked request");
        }

        await TypeHumanLikeAsync(page, "#sb_form_q", query);

        var startUrl = page.Url;
        await page.Keyboard.PressAsync("Enter");

        if (!await TryWaitForUrlChangeAsync(page, startUrl))
            return new SearchResult(false, [], "Bing navigation timeout");

        if (!page.Url.Contains("bing.com/search", StringComparison.OrdinalIgnoreCase))
            return new SearchResult(false, [], "Bing navigation to unexpected URL");

        try
        {
            await page.WaitForSelectorAsync(
                "#b_results",
                new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        catch
        {
            return new SearchResult(false, [], "Bing blocked request");
        }

        var json = await page.EvaluateAsync<string>(
                       @"
            JSON.stringify(Array.from(document.querySelectorAll('#b_results .b_algo')).slice(0, "
                       + maxResults + @").map(el => {
                const a = el.querySelector('h2 a');
                const snippet = el.querySelector('.b_caption p');
                const cite = el.querySelector('cite');
                let realUrl = '';
                if (cite) {
                    let citeText = cite.textContent.trim().replace(/\s/g, '');
                    if (citeText.includes('›')) {
                        citeText = citeText.split('›').map(s => s.trim()).join('/');
                        if (!citeText.startsWith('http')) citeText = 'https://' + citeText;
                    }
                    if (citeText.startsWith('http://') || citeText.startsWith('https://')) {
                        realUrl = citeText;
                    }
                }
                if (!realUrl && a) {
                    const href = a.getAttribute('href') || '';
                    if (href.startsWith('http') && !href.includes('bing.com/ck/')) {
                        realUrl = href;
                    } else {
                        try {
                            const params = new URLSearchParams(href.split('?')[1] || '');
                            const u = params.get('u') || '';
                            if (u) {
                                const payload = decodeURIComponent(u);
                                const idx = payload.indexOf('aHR0');
                                if (idx >= 0) {
                                    realUrl = atob(payload.substring(idx));
                                }
                            }
                        } catch(e) {}
                    }
                }
                return {
                    title: a?.textContent?.trim() ?? '',
                    url: realUrl,
                    snippet: snippet?.textContent?.trim() ?? ''
                };
            }))
        ");

        var raw = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json)!;
        var items = new List<SearchResultItem>();

        foreach (var r in raw)
        {
            var title = r["title"].GetString();
            var rawUrl = r["url"].GetString();
            var snippet = r["snippet"].GetString();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(rawUrl))
                continue;

            var url = rawUrl.Contains("bing.com/ck/", StringComparison.OrdinalIgnoreCase)
                          ? ExtractBingTrackingUrl(rawUrl)
                          : rawUrl;

            if (string.IsNullOrWhiteSpace(url) || url.Contains(
                    "bing.com/ck/",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            items.Add(new SearchResultItem(title, url, StripTags(snippet ?? "")));
        }

        return new SearchResult(
            items.Count > 0,
            items,
            items.Count == 0 ? "No Bing results matched" : null);
    }

    private async Task<SearchResult> SearchDdgAsync(IPage page, string query, int maxResults)
    {
        await page.GotoAsync(
            "https://duckduckgo.com/",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 15000 });

        try
        {
            await page.Locator("#searchbox_input")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        }
        catch
        {
            return new SearchResult(false, [], "Search engine blocked the request");
        }

        await TypeHumanLikeAsync(page, "#searchbox_input", query);

        var startUrl = page.Url;
        await page.Keyboard.PressAsync("Enter");

        if (!await TryWaitForUrlChangeAsync(page, startUrl))
            return new SearchResult(false, [], "DuckDuckGo navigation timeout");

        try
        {
            await page.WaitForSelectorAsync(
                "article[data-testid='result']",
                new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        catch
        {
            var json = await page.EvaluateAsync<string>(
                           @"
                JSON.stringify((() => {
                    const html = document.documentElement.outerHTML.toLowerCase();
                    return { blocked: html.includes('captcha') || html.includes('blocked'), items: [] };
                })())
            ");
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            if (parsed.TryGetValue("blocked", out var blocked) && blocked.GetBoolean())
                return new SearchResult(false, [], "Search engine blocked the request");
            return new SearchResult(false, [], "No DuckDuckGo results");
        }

        var resultsJson = await page.EvaluateAsync<string>(
                              @"
            JSON.stringify(Array.from(document.querySelectorAll('article[data-testid=""result""]'))
                .slice(0, " + maxResults + @")
                .map(article => {
                    const heading = article.querySelector('h2');
                    const link = heading?.querySelector('a');
                    const snippetEl = article.querySelector('[data-result=""snippet""]') ||
                                      article.querySelector('.snippet') ||
                                      article.querySelector('p');
                    return {
                        title: link?.textContent?.trim() ?? '',
                        url: link?.getAttribute('href') ?? '',
                        snippet: snippetEl?.textContent?.trim() ?? ''
                    };
                })
            )
        ");

        var items = new List<SearchResultItem>();
        using var doc = JsonDocument.Parse(resultsJson);
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            var title = r.GetProperty("title").GetString();
            var url = r.GetProperty("url").GetString();
            var snippet = r.GetProperty("snippet").GetString();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                continue;

            items.Add(
                new SearchResultItem(
                    System.Net.WebUtility.HtmlDecode(title),
                    System.Net.WebUtility.HtmlDecode(url),
                    StripTags(snippet ?? "")));
        }

        return new SearchResult(
            items.Count > 0,
            items,
            items.Count == 0 ? "No DuckDuckGo results" : null);
    }

    private static string StripTags(string html) => TagRegex().Replace(html, " ").Trim();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    private static async Task<bool> TryWaitForUrlChangeAsync(IPage page, string startUrl)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "start => window.location.href !== start",
                startUrl,
                new PageWaitForFunctionOptions { Timeout = 15000 });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task TypeHumanLikeAsync(IPage page, string selector, string text)
    {
        await page.Locator(selector).ClickAsync();
        await Task.Delay(Rng.Next(100, 300));

        foreach (var ch in text)
        {
            await page.Keyboard.TypeAsync(ch.ToString());
            await Task.Delay(Rng.Next(40, 120));
        }
    }
}
