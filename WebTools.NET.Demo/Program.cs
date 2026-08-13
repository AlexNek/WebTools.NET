using Microsoft.Extensions.DependencyInjection;

using WebTools.NET;
using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Search;

Console.WriteLine("=== WebTools.NET Demo ===");
Console.WriteLine();

// --- 1. URL Reachability Check (no browser needed) ---
Console.WriteLine("1. URL Reachability via HttpClient (WebAccessService)");
Console.WriteLine("   Checks if a URL is reachable by following redirects manually.");
Console.WriteLine();
using var webAccess = new WebAccessService();

var url1 = "https://github.com";
Console.WriteLine($"   Checking: {url1}");
var result1 = await webAccess.CheckReachabilityAsync(url1);
Console.WriteLine($"   → Reachable={result1.Reachable}, Status={result1.HttpStatus}, Redirects={result1.RedirectCount}, FinalUrl={result1.FinalUrl}");
Console.WriteLine();

var url2 = "https://this-does-not-exist-xyz123.com";
Console.WriteLine($"   Checking: {url2}");
var result2 = await webAccess.CheckReachabilityAsync(url2);
Console.WriteLine($"   → Reachable={result2.Reachable}, Status={result2.HttpStatus}, Error=\"{result2.ErrorMessage}\"");
Console.WriteLine();

// --- 2. DuckDuckGo search (HTTP-only, no browser) ---
Console.WriteLine("2. DuckDuckGo Search via plain HTTP (DuckDuckGoSearchProvider)");
Console.WriteLine("   Sends a GET request to html.duckduckgo.com and parses result links from HTML.");
Console.WriteLine("   No browser needed, but may be blocked by bot detection.");
Console.WriteLine();
using var ddg = new DuckDuckGoSearchProvider();

var ddgQuery = ".NET 10 new features";
Console.WriteLine($"   Query: \"{ddgQuery}\", MaxResults: 3");
var ddgResult = await ddg.SearchAsync(ddgQuery, maxResults: 3);
Console.WriteLine($"   → Success={ddgResult.Success}, ResultCount={ddgResult.Results.Count}, Error=\"{ddgResult.ErrorMessage ?? "(none)"}\"");
if (ddgResult.Results.Count > 0)
{
    foreach (var item in ddgResult.Results)
    {
        Console.WriteLine($"     • {item.Title}");
        Console.WriteLine($"       {item.Url}");
    }
}
Console.WriteLine();

// --- 3. Playwright search (browser-based, no stealth patches) ---
Console.WriteLine("3. Search via Playwright browser (PlaywrightSearchProvider)");
Console.WriteLine("   Launches headless Chromium via Playwright, navigates to Bing, types query,");
Console.WriteLine("   extracts results from DOM. Falls back to DuckDuckGo if Bing fails.");
Console.WriteLine("   Uses standard Playwright without stealth patches.");
Console.WriteLine();
await using var pwSearch = new PlaywrightSearchProvider(headless: true);

var pwQuery = ".NET 10 new features";
Console.WriteLine($"   Query: \"{pwQuery}\", MaxResults: 3");
var pwResult = await pwSearch.SearchAsync(pwQuery, maxResults: 3);
Console.WriteLine($"   → Success={pwResult.Success}, ResultCount={pwResult.Results.Count}, Error=\"{pwResult.ErrorMessage ?? "(none)"}\"");
if (pwResult.Results.Count > 0)
{
    foreach (var item in pwResult.Results)
    {
        Console.WriteLine($"     • {item.Title}");
        Console.WriteLine($"       {item.Url}");
    }
}
Console.WriteLine();

// --- 4. CloakBrowser search (stealth-patched browser - bypasses bot detection) ---
Console.WriteLine("4. Search via stealth browser (CloakBrowserSearchProvider)");
Console.WriteLine("   Launches a stealth-patched Chromium via CloakBrowser, navigates to Bing,");
Console.WriteLine("   types query, extracts results. Falls back to DuckDuckGo if Bing fails.");
Console.WriteLine("   Better at bypassing bot detection than plain Playwright.");
Console.WriteLine();
await using var cloakSearch = new CloakBrowserSearchProvider(headless: true);

var cloakQuery = ".NET 10 new features";
Console.WriteLine($"   Query: \"{cloakQuery}\", MaxResults: 3");
var cloakResult = await cloakSearch.SearchAsync(cloakQuery, maxResults: 3);
Console.WriteLine($"   → Success={cloakResult.Success}, ResultCount={cloakResult.Results.Count}, Error=\"{cloakResult.ErrorMessage ?? "(none)"}\"");
if (cloakResult.Results.Count > 0)
{
    foreach (var item in cloakResult.Results)
    {
        Console.WriteLine($"     • {item.Title}");
        Console.WriteLine($"       {item.Url}");
    }
}
Console.WriteLine();

// --- 5. CloakBrowser content fetching ---
Console.WriteLine("5. Page content fetching via stealth browser (CloakBrowserContentFetcher)");
Console.WriteLine("   Navigates to URL with stealth Chromium, waits for DOM, extracts text content.");
Console.WriteLine("   Handles 403 retries and Cloudflare challenges.");
Console.WriteLine();
await using var cloakFetcher = new CloakBrowserContentFetcher(headless: true);

var fetchUrl = "https://github.com";
Console.WriteLine($"   Fetching: {fetchUrl}");
var fetchResult = await cloakFetcher.FetchAsync(fetchUrl);
Console.WriteLine($"   → Success={fetchResult.Success}, ContentLength={fetchResult.Content.Length}, FinalUrl={fetchResult.FinalUrl}, Error=\"{fetchResult.ErrorMessage ?? "(none)"}\"");
if (fetchResult.Success && fetchResult.Content.Length > 0)
{
    var preview = fetchResult.Content[..Math.Min(200, fetchResult.Content.Length)];
    Console.WriteLine($"   Preview: {preview}...");
}
Console.WriteLine();

// --- 6. WebSearchAgent with CloakBrowser provider ---
Console.WriteLine("6. WebSearchAgent with automatic fallback queries");
Console.WriteLine("   Wraps any IWebSearchProvider. If first query returns empty,");
Console.WriteLine("   it generates modified queries (e.g. removes 'API', adds 'official site').");
Console.WriteLine();
await using var cloakSearch2 = new CloakBrowserSearchProvider(headless: true);
var searchAgent = new WebSearchAgent(cloakSearch2);

var agentQuery = "Playwright .NET API";
Console.WriteLine($"   Query: \"{agentQuery}\", MaxResults: 3");
var agentResult = await searchAgent.SearchAsync(agentQuery, maxResults: 3);
Console.WriteLine($"   → Success={agentResult.Success}, ResultCount={agentResult.Results.Count}, Error=\"{agentResult.ErrorMessage ?? "(none)"}\"");
if (agentResult.Results.Count > 0)
{
    foreach (var item in agentResult.Results)
    {
        Console.WriteLine($"     • {item.Title}");
        Console.WriteLine($"       {item.Url}");
    }
}
Console.WriteLine();

// --- 7. DI registration ---
Console.WriteLine("7. Dependency Injection registration");
Console.WriteLine("   AddWebToolsCore() registers IWebAccessService.");
Console.WriteLine("   AddBrowserServices(CloakBrowser) registers IWebContentFetcher, IWebSearchProvider, IBrowserInteraction.");
Console.WriteLine();
var services = new ServiceCollection();
services.AddWebToolsCore();
services.AddBrowserServices(EBrowserEngine.CloakBrowser);

var provider = services.BuildServiceProvider();
var resolved = provider.GetRequiredService<IWebAccessService>();
var resolvedFetcher = provider.GetRequiredService<IWebContentFetcher>();
var resolvedSearch = provider.GetRequiredService<IWebSearchProvider>();
var resolvedBrowser = provider.GetRequiredService<IBrowserInteraction>();
Console.WriteLine($"   IWebAccessService    → {resolved.GetType().Name}");
Console.WriteLine($"   IWebContentFetcher   → {resolvedFetcher.GetType().Name}");
Console.WriteLine($"   IWebSearchProvider   → {resolvedSearch.GetType().Name}");
Console.WriteLine($"   IBrowserInteraction  → {resolvedBrowser.GetType().Name}");
Console.WriteLine();

// --- 8. Geo region detection ---
Console.WriteLine("8. Geo region detection (GeoRegionAgent)");
Console.WriteLine("   Calls ip-api.com to get country code, maps to region (us/eu/china/intl).");
Console.WriteLine("   Falls back to system locale if API fails. Result is cached.");
Console.WriteLine();
var geoAgent = new GeoRegionAgent();
var region = await geoAgent.DetectRegionAsync();
Console.WriteLine($"   → Detected region: \"{region}\"");
Console.WriteLine();

Console.WriteLine("=== Demo complete ===");
