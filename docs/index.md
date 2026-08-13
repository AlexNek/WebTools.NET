# WebTools.NET Developer Manual

Web tools for .NET agents: web search, browser-based content fetching, and navigation.

WebTools.NET is a NuGet library that gives AI agents and automation tools
reliable access to the web — searching, fetching page content, checking URL
reachability, and autonomous navigation — with a choice of browser engines.

## Feature Overview

| Capability | Entry point | Description |
| --- | --- | --- |
| Web search | `WebSearchAgent`, `IWebSearchProvider` | Search the web via DuckDuckGo (HTTP) or browser-based providers |
| Content fetching | `IWebContentFetcher` | Retrieve rendered page content through headless browsers |
| URL reachability | `IWebAccessService` | Plain-HTTP reachability checks with redirect tracking |
| Navigation | `WebNavigationAgent` | Autonomous link extraction and same-host navigation |
| Geo-awareness | `GeoRegionAgent` | IP-based region detection with locale fallback |
| Dependency injection | `AddWebToolsCore()`, `AddBrowserServices()` | One-line integration via `IServiceCollection` extensions |

## Architecture at a Glance

All capabilities sit behind small abstractions in the `WebTools.NET.Abstractions`
namespace, so you can depend on interfaces and swap implementations:

```mermaid
graph LR
    A[WebSearchAgent] --> B[IWebSearchProvider]
    C[IWebAccessService] --> D[IWebContentFetcher]
    E[WebNavigationAgent] --> F[IBrowserInteraction]
    B --> G[DuckDuckGo / Playwright / CloakBrowser providers]
    D --> H[PlaywrightContentFetcher / CloakBrowserContentFetcher]
    F --> I[PlaywrightSession / CloakBrowserSession]
```

Every operation returns a result object (`SearchResult`, `WebContent`,
`UrlCheckResult`) instead of throwing — see
[Error Handling](concepts/error-handling.md).

## Where to Start

- New to the library? Follow [Installation](getting-started/installation.md)
  and the [Quick Start](getting-started/quick-start.md).
- Choosing between browser backends? Read
  [Browser Engines](concepts/browser-engines.md).
- Looking for a specific type? Check the
  [API Reference](api-reference/interfaces.md).

## License

MIT — see the [LICENSE.txt](https://github.com/AlexNek/WebTools.NET/blob/main/LICENSE.txt)
in the repository.
