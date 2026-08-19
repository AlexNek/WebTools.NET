# URL Reachability

URL reachability checks answer a simple question: *does this URL load?* Two
implementations exist, with different tradeoffs.

## Choosing a Checker

| Checker | Transport | Detects | Cost |
| --- | --- | --- | --- |
| `WebAccessService` (`IWebAccessService`) | Plain HTTP | HTTP status, redirects | Low |
| Content fetchers (`IWebContentFetcher`) | Rendered browser | HTTP status, browser error pages | High |

## WebAccessService — Plain HTTP

Registered by `AddWebToolsCore()`. Sends real browser-like headers, keeps a
cookie container, decompresses responses, and tracks redirects manually.

```csharp
var webAccess = provider.GetRequiredService<IWebAccessService>();

var check = await webAccess.CheckReachabilityAsync("https://test.example.com");
Console.WriteLine($"{check.Reachable} (HTTP {check.HttpStatus}, " +
                  $"{check.RedirectCount} redirects, final: {check.FinalUrl})");
```

Behavior details:

- Follows up to **10 redirects**; more yields `Reachable = false` with a
  "too many redirects" error
- A URL is reachable when the final status is `2xx` or `3xx` (excluding 304)
- 15 second overall request timeout; timeouts yield
  `ErrorMessage = "Timed out"`
- Handles both absolute and relative `Location` headers

!!! warning
    Plain HTTP cannot execute JavaScript. Pages that only fail inside a
    browser (bot walls, client-side errors) still report reachable here. Use
    a browser-based check when that matters.

## Browser-Based Checks

`IWebContentFetcher.CheckReachabilityAsync` loads the page in the configured
browser engine and reports `Reachable` only when the response status is `2xx`
and the final URL is not a browser error page.

```csharp
var check = await fetcher.CheckReachabilityAsync("https://test.example.com");
```

Useful for validating links discovered by the
[WebNavigationService](../navigation/web-navigation-service.md), which performs
exactly this kind of check internally.

## The UrlCheckResult Model

| Property | Description |
| --- | --- |
| `Reachable` | Whether the URL loaded successfully |
| `HttpStatus` | Final HTTP status code, when available |
| `ErrorMessage` | Failure reason, when not reachable |
| `RedirectCount` | Number of redirects followed |
| `FinalUrl` | URL after redirects |
| `ProtectionType` | Detected protection type, when reported by the engine |
