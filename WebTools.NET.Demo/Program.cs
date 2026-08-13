using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

using WebTools.NET;
using WebTools.NET.Abstractions;
using WebTools.NET.Demo;
using WebTools.NET.Geo;
using WebTools.NET.Models;
using WebTools.NET.Search;

// =====================================================================
//  WebTools.NET feature demo
//
//  Sections 1-4 are pure HTTP/DI (fast). From section 5 on, real Chromium
//  instances are launched, so those sections take noticeably longer.
//  DemoRunner isolates failures per section and prints a summary table
//  with per-section status and elapsed time at the end.
// =====================================================================

const string SearchQuery = ".NET 10 new features";
const string FetchUrl = "https://example.com";
const int MaxResults = 3;

ConsoleOutput.Banner(
    "WebTools.NET Demo",
    "Web search, browser content fetching, navigation and geo-awareness");
ConsoleOutput.Note("Browser-based sections launch headless Chromium and take a few seconds each.");
ConsoleOutput.BlankLine();

var runner = new DemoRunner();

// ---------------------------------------------------------------------
// 1. URL reachability via plain HTTP
// ---------------------------------------------------------------------
using var webAccess = new WebAccessService();

await runner.RunSectionAsync(
    "URL reachability via plain HTTP (WebAccessService)",
    "Follows redirects manually (max 10) - no browser needed.",
    async () =>
    {
        await CheckUrlAsync(url => webAccess.CheckReachabilityAsync(url), "https://github.com");
        await CheckUrlAsync(url => webAccess.CheckReachabilityAsync(url), "http://github.com");
        await CheckUrlAsync(url => webAccess.CheckReachabilityAsync(url), "https://this-does-not-exist-xyz123.com");
    });

// ---------------------------------------------------------------------
// 2. Geo region detection
// ---------------------------------------------------------------------
using var geoAgent = new GeoRegionAgent();

await runner.RunSectionAsync(
    "Geo region detection (GeoRegionAgent)",
    "Geo-IP lookup via ip-api.com with system-locale fallback. Result is cached.",
    async () =>
    {
        var watch = Stopwatch.StartNew();
        var region = await geoAgent.DetectRegionAsync();
        watch.Stop();
        ConsoleOutput.Ok($"detected region \"{region}\" ({watch.ElapsedMilliseconds} ms - Geo-IP lookup)");

        watch.Restart();
        var cached = await geoAgent.DetectRegionAsync();
        watch.Stop();
        ConsoleOutput.Ok($"detected region \"{cached}\" ({watch.ElapsedMilliseconds} ms - served from cache)");
    });

// ---------------------------------------------------------------------
// 3. DuckDuckGo search over plain HTTP
// ---------------------------------------------------------------------
using var ddg = new DuckDuckGoSearchProvider();

await runner.RunSectionAsync(
    "Web search via plain HTTP (DuckDuckGoSearchProvider)",
    "GETs html.duckduckgo.com and parses result links - no browser, but may hit bot detection.",
    () => SearchDemoAsync(ddg.SearchAsync, SearchQuery));

// ---------------------------------------------------------------------
// 4. Dependency injection
// ---------------------------------------------------------------------
var services = new ServiceCollection();
services.AddWebToolsCore();
services.AddBrowserServices(EBrowserEngine.Playwright);
await using var sp = services.BuildServiceProvider();

var fetcher = sp.GetRequiredService<IWebContentFetcher>();
var search = sp.GetRequiredService<IWebSearchProvider>();
var browser = sp.GetRequiredService<IBrowserInteraction>();

await runner.RunSectionAsync(
    "Dependency injection (AddWebToolsCore + AddBrowserServices)",
    "One call per concern; the engine enum selects the concrete implementations.",
    () =>
    {
        ConsoleOutput.Info("IWebAccessService ", sp.GetRequiredService<IWebAccessService>().GetType().Name);
        ConsoleOutput.Info("IWebContentFetcher", fetcher.GetType().Name);
        ConsoleOutput.Info("IWebSearchProvider", search.GetType().Name);
        ConsoleOutput.Info("IBrowserInteraction", browser.GetType().Name);
        ConsoleOutput.Ok("all services resolved for engine: Playwright");
        return Task.CompletedTask;
    });

// ---------------------------------------------------------------------
// 5. Content fetching through a real browser
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "Page content fetching via browser (IWebContentFetcher)",
    "Navigates with headless Chromium, waits for the DOM, extracts readable text.",
    () => FetchDemoAsync(fetcher, FetchUrl));

// ---------------------------------------------------------------------
// 6. Browser-based reachability + bot protection detection
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "Browser-based reachability (IWebContentFetcher.CheckReachabilityAsync)",
    "Renders the page in Chromium, so JS redirects and bot walls are detected.",
    async () =>
    {
        await CheckUrlAsync(url => fetcher.CheckReachabilityAsync(url), FetchUrl, showProtection: true);
        await CheckUrlAsync(url => fetcher.CheckReachabilityAsync(url), "https://github.com", showProtection: true);
    });

// ---------------------------------------------------------------------
// 7. Browser-based search
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "Web search via browser (IWebSearchProvider)",
    "Types the query into Bing like a human and scrapes the rendered results.",
    () => SearchDemoAsync(search.SearchAsync, SearchQuery));

// ---------------------------------------------------------------------
// 8. WebSearchAgent - automatic fallback queries
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "WebSearchAgent with automatic fallback queries",
    "Wraps any IWebSearchProvider; on empty results it retries with modified queries.",
    async () =>
    {
        await using var agent = new WebSearchAgent(search);
        await SearchDemoAsync(
            agent.SearchAsync,
            "Playwright .NET API",
            "PlaywrightSearchProvider (reuses the browser from section 7)");
    });

// ---------------------------------------------------------------------
// 9. CloakBrowser stealth engine
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "CloakBrowser stealth engine (engine switch via DI)",
    "Stealth-patched Chromium resists bot detection. Same interfaces, different engine.",
    async () =>
    {
        var cloakServices = new ServiceCollection();
        cloakServices.AddBrowserServices(EBrowserEngine.CloakBrowser);
        await using var cloakProvider = cloakServices.BuildServiceProvider();

        var cloakSearch = cloakProvider.GetRequiredService<IWebSearchProvider>();
        var cloakFetcher = cloakProvider.GetRequiredService<IWebContentFetcher>();
        ConsoleOutput.Info("IWebSearchProvider ", cloakSearch.GetType().Name);
        ConsoleOutput.Info("IWebContentFetcher", cloakFetcher.GetType().Name);

        await SearchDemoAsync(cloakSearch.SearchAsync, SearchQuery);
        ConsoleOutput.BlankLine();
        await FetchDemoAsync(cloakFetcher, FetchUrl);
    });

// ---------------------------------------------------------------------
// 10. Interactive browsing
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "Interactive browsing (IBrowserInteraction)",
    "NavigateAsync -> FillAsync -> ClickAsync -> read the resulting page.",
    async () =>
    {
        const string startUrl = "https://en.wikipedia.org/wiki/Special:Search";
        const string term = "WebTools.NET";

        await browser.NavigateAsync(startUrl);
        ConsoleOutput.Detail($"navigated to {startUrl}");

        await browser.FillAsync("#search input[name=search]", term);
        ConsoleOutput.Detail($"filled search box with \"{term}\"");

        await browser.ClickAsync("#search button[type=submit]");
        ConsoleOutput.Detail("clicked submit");

        var currentUrl = await browser.GetCurrentUrlAsync();
        ConsoleOutput.Ok($"current url  : {currentUrl}");

        var html = await browser.GetHtmlAsync();
        ConsoleOutput.Ok($"page html    : {html.Length:N0} chars");

        var text = await browser.GetContentAsync();
        ConsoleOutput.Detail($"page text    : {text.Length:N0} chars");
        ConsoleOutput.Detail($"preview      : {ConsoleOutput.Preview(text)}");
    });

// ---------------------------------------------------------------------
// 11. Web navigation agent
// ---------------------------------------------------------------------
await runner.RunSectionAsync(
    "WebNavigationAgent - link extraction and validation",
    "Extracts same-host links from a page and verifies each one in the browser.",
    async () =>
    {
        await using var navAgent = new WebNavigationAgent(browser);
        const string startUrl = "https://en.wikipedia.org/wiki/.NET";
        ConsoleOutput.Info("start url", startUrl);
        ConsoleOutput.Info("limit", "first 5 same-host links");

        var working = await navAgent.NavigateAsync(startUrl, maxLinks: 5);
        if (working.Count == 0)
        {
            ConsoleOutput.Fail("no working links found");
            return;
        }

        ConsoleOutput.Ok($"{working.Count} working link(s):");
        foreach (var link in working)
        {
            ConsoleOutput.Detail($"  {link}");
        }
    });

runner.PrintSummary();

return runner.AllSucceeded ? 0 : 1;

// ---------------------------------------------------------------------
//  Shared demo steps
// ---------------------------------------------------------------------
async Task CheckUrlAsync(
    Func<string, Task<UrlCheckResult>> check,
    string url,
    bool showProtection = false)
{
    ConsoleOutput.Info("URL", url);
    var result = await check(url);

    if (result.Reachable)
    {
        var protection = showProtection
            ? $", bot protection: {(string.IsNullOrEmpty(result.ProtectionType) ? "none detected" : result.ProtectionType)}"
            : string.Empty;
        ConsoleOutput.Ok($"reachable - HTTP {(int?)result.HttpStatus}{protection}");

        if (result.RedirectCount > 0)
        {
            ConsoleOutput.Detail($"redirects: {result.RedirectCount}");
            ConsoleOutput.Detail($"final url: {result.FinalUrl}");
        }
    }
    else
    {
        ConsoleOutput.Fail($"unreachable - {result.ErrorMessage}");
    }

    ConsoleOutput.BlankLine();
}

async Task SearchDemoAsync(
    Func<string, int, CancellationToken, Task<SearchResult>> searchAsync,
    string query,
    string? providerNote = null)
{
    ConsoleOutput.Info("query", $"\"{query}\" (max {MaxResults} results)");
    if (providerNote is not null)
    {
        ConsoleOutput.Info("provider", providerNote);
    }

    var result = await searchAsync(query, MaxResults, CancellationToken.None);
    ConsoleOutput.PrintSearchResult(result);
}

async Task FetchDemoAsync(IWebContentFetcher fetcher, string url)
{
    ConsoleOutput.Info("url", url);
    var content = await fetcher.FetchAsync(url);
    if (!content.Success)
    {
        ConsoleOutput.Fail($"fetch failed: {content.ErrorMessage}");
        return;
    }

    ConsoleOutput.Ok($"fetched {content.Content.Length:N0} chars of text");
    ConsoleOutput.Detail($"final url: {content.FinalUrl}");
    ConsoleOutput.Detail($"preview  : {ConsoleOutput.Preview(content.Content)}");
}
