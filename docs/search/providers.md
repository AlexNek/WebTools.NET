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

## CloakBrowserSearchProvider

Same browser-based approach on the CloakBrowser engine with stealth scripts
enabled — see [Browser Engines](../concepts/browser-engines.md).

!!! warning
    Browser-based providers are significantly slower than the HTTP provider
    and consume more resources. Prefer `DuckDuckGoSearchProvider` unless its
    results are insufficient for your scenario.
