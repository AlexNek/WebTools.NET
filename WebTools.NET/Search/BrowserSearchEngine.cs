using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Search;

/// <summary>
/// Shared browser-based search orchestration (Bing primary, DuckDuckGo fallback)
/// used by the Playwright and CloakBrowser search providers. Pages are supplied
/// by an engine-specific factory.
/// </summary>
internal sealed class BrowserSearchEngine
{
    private const int EngineSwitchDelayMs = 3000;

    private const int PreSearchDelayMaxMs = 2000;

    private const int PreSearchDelayMinMs = 500;

    private const int SearchGotoTimeoutMs = 15000;

    private const int SelectorWaitTimeoutMs = 10000;

    private const int TypePauseMaxMs = 120;

    private const int TypePauseMinMs = 40;

    private const int TypeStartDelayMaxMs = 300;

    private const int TypeStartDelayMinMs = 100;

    private const int UrlChangeTimeoutMs = 15000;

    internal static readonly Random Rng = new();

    internal static readonly string[] UserAgents =
        [
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
        ];

    private readonly string _engineName;

    private readonly Func<CancellationToken, Task<ISearchPageLease>>? _getFallbackPageLeaseAsync;

    private readonly Func<int, CancellationToken, Task> _delayAsync;

    private readonly Func<CancellationToken, Task<IPage>> _getPageAsync;

    private readonly ILogger? _logger;

    internal BrowserSearchEngine(
        Func<CancellationToken, Task<IPage>> getPageAsync,
        ILogger? logger,
        string engineName,
        Func<CancellationToken, Task<ISearchPageLease>>? getFallbackPageLeaseAsync = null,
        Func<int, CancellationToken, Task>? delayAsync = null)
    {
        _getPageAsync = getPageAsync ?? throw new ArgumentNullException(nameof(getPageAsync));
        _logger = logger;
        _engineName = engineName;
        _getFallbackPageLeaseAsync = getFallbackPageLeaseAsync;
        _delayAsync = delayAsync ?? ((milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken));
    }

    internal bool HasFallbackFactory => _getFallbackPageLeaseAsync is not null;

    internal async Task<SearchResult> SearchAsync(
        string query,
        int maxResults,
        CancellationToken ct)
    {
        try
        {
            await _delayAsync(Rng.Next(PreSearchDelayMinMs, PreSearchDelayMaxMs), ct);

            await using var page = await _getPageAsync(ct);
            var primary = await SearchChainAsync(page, query, maxResults, ct);
            if (IsUsable(primary.Result))
            {
                return primary.Result;
            }

            if (ShouldUseFallback(primary.Bing, primary.DuckDuckGo, _getFallbackPageLeaseAsync is not null))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await using var fallbackLease = await _getFallbackPageLeaseAsync!(ct);
                    var fallback = await SearchChainAsync(fallbackLease.Page, query, maxResults, ct);
                    if (IsUsable(fallback.Result))
                    {
                        return fallback.Result;
                    }

                    return CombineFallbackFailures(primary.Result, fallback.Result);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        ex,
                        "{Engine} non-headless fallback error for query '{Query}'",
                        _engineName,
                        query);
                    return CombineFallbackFailures(
                        primary.Result,
                        new SearchResult(false, [], $"{_engineName} fallback error: {ex.Message}"));
                }
            }

            return primary.Result;
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogError(ex, "{Engine} error for query '{Query}'", _engineName, query);
            return new SearchResult(false, [], $"{_engineName} error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return new SearchResult(false, [], "Search timed out");
        }
    }

    internal static bool ShouldUseFallback(
        SearchAttemptOutcome? bing,
        SearchAttemptOutcome? duckDuckGo,
        bool fallbackAvailable) =>
        fallbackAvailable &&
        bing?.FailureKind == SearchFailureKind.Blocked &&
        duckDuckGo?.FailureKind == SearchFailureKind.Blocked;

    private async Task<(SearchAttemptOutcome Bing, SearchAttemptOutcome? DuckDuckGo, SearchResult Result)> SearchChainAsync(
        IPage page,
        string query,
        int maxResults,
        CancellationToken ct)
    {
        var bing = await SearchBingAsync(page, query, maxResults, ct);
        if (IsUsable(bing.Result))
        {
            return (bing, null, bing.Result);
        }

        await _delayAsync(EngineSwitchDelayMs, ct);
        var duckDuckGo = await SearchDdgAsync(page, query, maxResults, ct);
        if (IsUsable(duckDuckGo.Result))
        {
            return (bing, duckDuckGo, duckDuckGo.Result);
        }

        return (bing, duckDuckGo, CombinePrimaryFailures(bing.Result, duckDuckGo.Result));
    }

    private static bool IsUsable(SearchResult result) =>
        result.Success && result.Results.Count > 0;

    private static SearchResult CombinePrimaryFailures(
        SearchResult bing,
        SearchResult duckDuckGo)
    {
        var bingError = bing.ErrorMessage ?? "No Bing results";
        var duckDuckGoError = duckDuckGo.ErrorMessage ?? "No DuckDuckGo results";
        return new SearchResult(
            false,
            [],
            $"{bingError}; DuckDuckGo fallback: {duckDuckGoError}");
    }

    private static SearchResult CombineFallbackFailures(
        SearchResult primary,
        SearchResult fallback)
    {
        var primaryError = primary.ErrorMessage ?? "Primary search failed";
        var fallbackError = fallback.ErrorMessage ?? "No fallback results";
        return new SearchResult(
            false,
            [],
            $"{primaryError}; Non-headless fallback: {fallbackError}");
    }

    private async Task<SearchAttemptOutcome> SearchBingAsync(
        IPage page,
        string query,
        int maxResults,
        CancellationToken ct)
    {
        try
        {
            await page.GotoAsync(
                "https://www.bing.com/",
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = SearchGotoTimeoutMs
                })
                .AwaitWithCancellationAsync(ct);

            try
            {
                await page.Locator("#sb_form_q")
                    .WaitForAsync(new LocatorWaitForOptions { Timeout = SelectorWaitTimeoutMs })
                    .AwaitWithCancellationAsync(ct);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return await CreateSelectorFailureAsync(
                    page,
                    "Bing blocked request",
                    "Bing search form unavailable",
                    SearchFailureKind.Other,
                    ct);
            }

            await TypeHumanLikeAsync(page, "#sb_form_q", query, ct);

            var startUrl = page.Url;
            await page.Keyboard.PressAsync("Enter").AwaitWithCancellationAsync(ct);

            if (!await TryWaitForUrlChangeAsync(page, startUrl, ct))
            {
                return Failure("Bing navigation timeout", SearchFailureKind.Timeout);
            }

            if (!page.Url.Contains("bing.com/search", StringComparison.OrdinalIgnoreCase))
            {
                if (await IsBlockingPageAsync(page, ct))
                {
                    return Failure("Bing blocked request", SearchFailureKind.Blocked);
                }

                return Failure("Bing navigation to unexpected URL", SearchFailureKind.Other);
            }

            try
            {
                await page.WaitForSelectorAsync(
                    "#b_results",
                    new PageWaitForSelectorOptions { Timeout = SelectorWaitTimeoutMs })
                .AwaitWithCancellationAsync(ct);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return await CreateSelectorFailureAsync(
                    page,
                    "Bing blocked request",
                    "No Bing results matched",
                    SearchFailureKind.NoResults,
                    ct);
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
        ")
                .AwaitWithCancellationAsync(ct);

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

                items.Add(new SearchResultItem(title, url, HtmlUtils.StripTags(snippet ?? "")));
            }

            return items.Count > 0
                ? new SearchAttemptOutcome(new SearchResult(true, items, null), SearchFailureKind.None)
                : Failure("No Bing results matched", SearchFailureKind.NoResults);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return Failure("Bing navigation timeout", SearchFailureKind.Timeout);
        }
        catch (PlaywrightException ex)
        {
            return Failure($"Bing error: {ex.Message}", SearchFailureKind.Other);
        }
    }

    private async Task<SearchAttemptOutcome> SearchDdgAsync(
        IPage page,
        string query,
        int maxResults,
        CancellationToken ct)
    {
        try
        {
            await page.GotoAsync(
                "https://duckduckgo.com/",
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = SearchGotoTimeoutMs
                })
                .AwaitWithCancellationAsync(ct);

            try
            {
                await page.Locator("#searchbox_input")
                    .WaitForAsync(new LocatorWaitForOptions { Timeout = SelectorWaitTimeoutMs })
                    .AwaitWithCancellationAsync(ct);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return await CreateSelectorFailureAsync(
                    page,
                    "Search engine blocked the request",
                    "DuckDuckGo search box unavailable",
                    SearchFailureKind.Other,
                    ct);
            }

            await TypeHumanLikeAsync(page, "#searchbox_input", query, ct);

            var startUrl = page.Url;
            await page.Keyboard.PressAsync("Enter").AwaitWithCancellationAsync(ct);

            if (!await TryWaitForUrlChangeAsync(page, startUrl, ct))
            {
                return Failure("DuckDuckGo navigation timeout", SearchFailureKind.Timeout);
            }

            try
            {
                await page.WaitForSelectorAsync(
                    "article[data-testid='result']",
                    new PageWaitForSelectorOptions { Timeout = SelectorWaitTimeoutMs })
                .AwaitWithCancellationAsync(ct);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                if (await IsBlockingPageAsync(page, ct))
                {
                    return Failure("Search engine blocked the request", SearchFailureKind.Blocked);
                }

                return Failure("No DuckDuckGo results", SearchFailureKind.NoResults);
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
        ")
                .AwaitWithCancellationAsync(ct);

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
                        HtmlUtils.StripTags(snippet ?? "")));
            }

            return items.Count > 0
                ? new SearchAttemptOutcome(new SearchResult(true, items, null), SearchFailureKind.None)
                : Failure("No DuckDuckGo results", SearchFailureKind.NoResults);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return Failure("DuckDuckGo navigation timeout", SearchFailureKind.Timeout);
        }
        catch (PlaywrightException ex)
        {
            return Failure($"DuckDuckGo error: {ex.Message}", SearchFailureKind.Other);
        }
    }

    private static async Task<SearchAttemptOutcome> CreateSelectorFailureAsync(
        IPage page,
        string blockedMessage,
        string nonBlockedMessage,
        SearchFailureKind nonBlockedKind,
        CancellationToken ct)
    {
        return await IsBlockingPageAsync(page, ct)
            ? Failure(blockedMessage, SearchFailureKind.Blocked)
            : Failure(nonBlockedMessage, nonBlockedKind);
    }

    private static async Task<bool> IsBlockingPageAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var content = await page.EvaluateAsync<string>(
                @"(() => {
                    const title = document.title || '';
                    const html = document.documentElement?.outerHTML || '';
                    return (title + '\n' + html).toLowerCase();
                })()")
                .AwaitWithCancellationAsync(ct);

            string[] markers =
            [
                "captcha",
                "access denied",
                "verify you are human",
                "unusual traffic",
                "robot check",
                "challenge-platform",
                "cf-chl-",
                "blocked request"
            ];

            return markers.Any(content.Contains);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (page is not null)
        {
            return false;
        }
    }

    private static SearchAttemptOutcome Failure(string message, SearchFailureKind failureKind) =>
        new(new SearchResult(false, [], message), failureKind);

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

    private static async Task<bool> TryWaitForUrlChangeAsync(
        IPage page,
        string startUrl,
        CancellationToken ct)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "start => window.location.href !== start",
                startUrl,
                new PageWaitForFunctionOptions { Timeout = UrlChangeTimeoutMs })
                .AwaitWithCancellationAsync(ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task TypeHumanLikeAsync(
        IPage page,
        string selector,
        string text,
        CancellationToken ct)
    {
        await page.Locator(selector).ClickAsync().AwaitWithCancellationAsync(ct);
        await _delayAsync(Rng.Next(TypeStartDelayMinMs, TypeStartDelayMaxMs), ct);

        foreach (var ch in text)
        {
            await page.Keyboard.TypeAsync(ch.ToString()).AwaitWithCancellationAsync(ct);
            await _delayAsync(Rng.Next(TypePauseMinMs, TypePauseMaxMs), ct);
        }
    }
}
