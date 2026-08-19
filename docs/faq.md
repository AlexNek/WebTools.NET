# FAQ

## Which search provider should I use?

Start with `DuckDuckGoSearchProvider` — it is the only one that needs no
browser and is by far the cheapest. Move to a browser-based provider
(`PlaywrightSearchProvider`, `CloakBrowserSearchProvider`) only when the HTML
endpoint does not give usable results for your queries. See
[Search Providers](search/providers.md).

## When do I need the CloakBrowser engine?

When target pages detect and block plain Playwright automation (bot walls,
"please verify you are human" interstitials). `CloakBrowserSession` launches
through CloakBrowser and applies stealth scripts that mask common automation
signals. See [Browser Engines](concepts/browser-engines.md).

## Do I need Playwright browser binaries for every feature?

No. `DuckDuckGoSearchProvider` and `WebAccessService` are plain HTTP. Only
content fetching, browser search providers, and `WebNavigationService` drive a
browser. See [Installation](getting-started/installation.md).

## Why does my fetch/search return `Success = false` without an exception?

By design. WebTools.NET reports operational failures through result objects
(`Success` / `Reachable` flags with `ErrorMessage`) instead of throwing. See
[Error Handling](concepts/error-handling.md).

## Why does NavigateAsync return only same-host links?

The navigation service is designed for exploring one site at a time. Cross-host
links are dropped during extraction, and returned links are verified for
reachability first. See
[WebNavigationService](navigation/web-navigation-service.md).

## Can I use the library without dependency injection?

Yes. Browser-backed services accept caller-supplied browser sessions or
providers, and the caller manages those dependencies. See
[Dependency Injection](getting-started/dependency-injection.md#manual-construction-without-di).

## How do I debug what the services are doing?

Pass an `ILogger<T>` to the constructor (or register logging before
`AddBrowserServices`) and enable `Debug` level for `WebTools.NET` types.
Navigation steps, extracted links, fallback queries, and failures are logged
at debug level.

## How accurate is the geo-region detection?

Best effort. The Geo-IP lookup uses a free plain-HTTP tier and falls back to
the system UI culture on failure. Treat the result as a hint for choosing
endpoints, never as a security decision. See
[Geo-awareness](navigation/geo-region.md).
