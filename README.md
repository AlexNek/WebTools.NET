# WebTools.NET

Web tools for .NET agents: web search, browser-based content fetching, and navigation.

WebTools.NET gives AI agents and automation scripts reliable access to the real
web — searching, fetching rendered page content, checking URL reachability, and
autonomous link navigation — through a small set of async, interface-based APIs.

## Why WebTools.NET?

`HttpClient` alone is not enough for web-facing agents:

- **JavaScript-rendered pages** come back empty — the content you need is
  built by scripts after load.
- **Bot protection** blocks plain HTTP clients and even default headless
  browsers.
- **Search engines** have no stable free API, so scraping them by hand is
  brittle.
- **Redirect chains, timeouts, and error handling** get reimplemented in every
  project.

WebTools.NET solves this by driving real browsers (Playwright Chromium, or the
stealth-patched CloakBrowser) behind small abstractions, while keeping
plain-HTTP fast paths for the cases where a browser is overkill. Every
operation returns a result object (`SearchResult`, `WebContent`,
`UrlCheckResult`) instead of throwing, so agent code can reason about failures
and retry or fall back on its own terms.

## Feature Overview

| Capability | Entry point | Description |
| --- | --- | --- |
| Web search | `WebSearchAgent`, `IWebSearchProvider` | DuckDuckGo over plain HTTP, or search driven by a real browser |
| Content fetching | `IWebContentFetcher` | Rendered page text extracted through a headless browser |
| URL reachability | `IWebAccessService` | Plain-HTTP check with redirect tracking — no browser needed |
| Interactive browsing | `IBrowserInteraction` | Navigate, fill forms, click, and read the resulting page |
| Autonomous navigation | `WebNavigationAgent` | Same-host link extraction and verification |
| Geo-awareness | `GeoRegionAgent` | IP-based region detection with locale fallback, cached |
| Dependency injection | `AddWebToolsCore()`, `AddBrowserServices()` | One-line `IServiceCollection` integration |

## Installation

```bash
dotnet add package WebTools.NET
```

Requires .NET 10.0 or later. Browser-based features drive Chromium through
Microsoft Playwright; install the browser binaries once per machine:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

`DuckDuckGoSearchProvider` and `IWebAccessService` use plain HTTP and need no
browser.

## Quick Start

Register the services you need — one call per concern:

```csharp
using Microsoft.Extensions.DependencyInjection;
using WebTools.NET.Abstractions;

var services = new ServiceCollection();

services.AddWebToolsCore();   // IWebAccessService
services.AddBrowserServices(); // IWebContentFetcher, IWebSearchProvider,
                               // IBrowserInteraction (Playwright by default)

await using var provider = services.BuildServiceProvider();
```

### Check URL reachability (no browser)

```csharp
var webAccess = provider.GetRequiredService<IWebAccessService>();

var check = await webAccess.CheckReachabilityAsync("https://test.example.com");
Console.WriteLine(check.Reachable
    ? $"reachable - HTTP {check.HttpStatus}, {check.RedirectCount} redirect(s)"
    : $"unreachable - {check.ErrorMessage}");
```

### Fetch page content through a real browser

```csharp
var fetcher = provider.GetRequiredService<IWebContentFetcher>();

var content = await fetcher.FetchAsync("https://test.example.com");
if (content.Success)
{
    Console.WriteLine(content.FinalUrl);  // URL after redirects
    Console.WriteLine(content.Content);   // plain-text rendered page content
}
```

### Search the web

`WebSearchAgent` wraps any `IWebSearchProvider` and automatically retries with
fallback queries when the first attempt returns nothing:

```csharp
using WebTools.NET;
using WebTools.NET.Search;

using var ddg = new DuckDuckGoSearchProvider();   // plain HTTP, no browser
await using var search = new WebSearchAgent(ddg);

var result = await search.SearchAsync("dotnet web scraping", maxResults: 5);
if (result.Success)
{
    foreach (var item in result.Results)
    {
        Console.WriteLine($"{item.Title} -> {item.Url}");
    }
}
```

For sites behind bot protection, resolve `IWebSearchProvider` from DI instead —
the browser-based providers type the query into a real search page and scrape
the rendered results.

### Drive a page interactively

```csharp
var browser = provider.GetRequiredService<IBrowserInteraction>();

await browser.NavigateAsync("https://test.example.com/search");
await browser.FillAsync("input[name=q]", "WebTools.NET");
await browser.ClickAsync("button[type=submit]");

var url  = await browser.GetCurrentUrlAsync();
var text = await browser.GetContentAsync();   // readable text of the result page
```

### Verify links on a page

`WebNavigationAgent` extracts same-host links from a page and verifies each one
in the browser:

```csharp
await using var navAgent = new WebNavigationAgent();   // owns its own browser

var workingLinks = await navAgent.NavigateAsync("https://test.example.com", maxLinks: 20);
foreach (var link in workingLinks)
{
    Console.WriteLine(link);
}
```

### Detect the caller's region

```csharp
using WebTools.NET.Geo;

using var geo = new GeoRegionAgent();
var region = await geo.DetectRegionAsync();   // e.g. "DE" - Geo-IP with locale fallback, cached
```

## Choosing a Browser Engine

Browser services are engine-agnostic — the same interfaces work with either
backend, selected with one enum:

```csharp
services.AddBrowserServices(EBrowserEngine.Playwright);    // default
services.AddBrowserServices(EBrowserEngine.CloakBrowser);  // stealth-patched Chromium,
                                                           // resists bot detection
services.AddBrowserServices(EBrowserEngine.Playwright, headless: false);
```

Rule of thumb: use Playwright for normal automation, CloakBrowser when target
sites detect and block headless browsers.

## Design Highlights

- **Interface-based** — all capabilities sit behind abstractions in
  `WebTools.NET.Abstractions`, easy to mock in tests.
- **Engine-agnostic** — swap Playwright for CloakBrowser (or your own
  implementation) without touching calling code.
- **Result objects, not exceptions** — every operation reports success,
  error message, and payload in one record.
- **DI-first, but constructor-friendly** — agents can also be created directly
  and manage their own browser lifetime.
- **Fully async with `CancellationToken` support** throughout.

## Demo Project

The repository contains `WebTools.NET.Demo`, a console app that exercises every
feature end to end: reachability checks, geo detection, HTTP and browser
search, content fetching, the stealth engine, interactive browsing, and link
navigation — with a per-section summary table at the end.

## Documentation

The developer manual is published at
[alexnek.github.io/WebTools.NET](https://alexnek.github.io/WebTools.NET/).

## Changelog

See [CHANGELOG.md](https://github.com/AlexNek/WebTools.NET/blob/main/CHANGELOG.md) for release history.

## License

MIT – see [LICENSE.txt](https://github.com/AlexNek/WebTools.NET/blob/main/LICENSE.txt) for details.
