# Search Providers

Three `IWebSearchProvider` implementations ship with WebTools.NET.

## Comparison

| Provider | Transport | Browser needed | Stealth | Typical use |
| --- | --- | --- | --- | --- |
| `DuckDuckGoSearchProvider` | Plain HTTP | No | — | Fast, lightweight default choice |
| `PlaywrightSearchProvider` | Chromium via Playwright | Yes | No | When DuckDuckGo HTML results are insufficient |
| `CloakBrowserSearchProvider` | Chromium via CloakBrowser | Yes | Yes | Anti-bot protected search endpoints |

## DuckDuckGoSearchProvider

Parses DuckDuckGo's HTML-only endpoint (`html.duckduckgo.com`), so no browser
is involved.

- 10 second HTTP timeout, browser-like `User-Agent` header
- Accepts an `HttpClient` in the constructor for testing or custom
  configuration
- Implements `IDisposable` (disposes its owned `HttpClient` when constructed
  without one)

```csharp
using var provider = new DuckDuckGoSearchProvider();
var result = await provider.SearchAsync("playwright automation");
```

## PlaywrightSearchProvider

Performs the search in a real Chromium session and extracts results from the
rendered page. Registered by `AddBrowserServices()` when the Playwright
engine is selected.

## Headless fallback behavior

Visible-browser fallback is disabled by default because opening a window is an
explicit application choice. Enable it with
`enableVisibleSearchFallback: true` on `AddBrowserServices`,
`PlaywrightSearchProvider`, or `CloakBrowserSearchProvider`.

When enabled for a provider created with `headless: true`, it may make one
additional search attempt in a temporary visible Chromium browser. This retry
is started only when both the Bing and DuckDuckGo attempts in the headless
browser are classified as bot-blocked. It is not started for no results,
timeouts, ordinary browser/network failures, or markup changes without blocking
evidence.

The temporary visible browser, context, and page are disposed after the retry.
Visible fallback work is serialized per provider instance, so concurrent blocked
searches do not launch unbounded visible browsers. A provider created with
`headless: false` does not create a second browser. A visible-browser launch
failure is returned as a failed `SearchResult`; it is not thrown as an unhandled
browser-resource exception. Cancellation is checked before the fallback begins,
so an already-canceled request does not open a visible browser.

This behavior requires an environment where a graphical Chromium instance can
run. Applications that cannot open a visible browser should use the plain HTTP
provider or leave visible fallback disabled and handle the failed `SearchResult`
explicitly.

## CloakBrowserSearchProvider

Same browser-based approach on the CloakBrowser engine with stealth scripts
enabled — see [Browser Engines](../concepts/browser-engines.md).

!!! warning
    Browser-based providers are significantly slower than the HTTP provider
    and consume more resources. Prefer `DuckDuckGoSearchProvider` unless its
    results are insufficient for your scenario.
