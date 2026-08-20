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
browser engine, waits for post-load navigation to settle, and reports
`Reachable` only when the response status is successful or an accepted redirect
and the final URL is not a browser error page. `FinalUrl` is the browser's URL
after that wait, so it includes a JavaScript redirect or other browser-side
navigation that occurs after the initial response.

```csharp
var check = await fetcher.CheckReachabilityAsync("https://test.example.com");
```

### Example: JavaScript redirects

Suppose the requested URL is `https://test.example.com/pricing`, the server
returns HTTP 200, and the page later runs:

```javascript
window.location.replace("/");
```

A browser-based check observes the rendered result:

```text
Reachable            = true
HttpStatus           = 200
RedirectCount        = 0
ClientRedirectCount  = 1
FinalUrl             = https://test.example.com/
```

`FinalUrl` is captured after the browser has waited for post-load navigation,
not immediately when the initial HTTP response arrives. `ClientRedirectCount`
is intentionally limited to `0` or `1`: it indicates that a same-host
client-side URL change was observed. A cross-host client-side navigation still
updates `FinalUrl`, but leaves `ClientRedirectCount` at `0`.

Browser checks also distinguish server-side and client-side redirects:

- `RedirectCount` represents HTTP redirect hops tracked by
  `WebAccessService`. Browser content fetchers do not populate this count.
- `ClientRedirectCount` is `1` when the browser URL changes to another URL on
  the same host after the initial navigation; otherwise it is `0`.
- A cross-host client-side navigation is still returned in `FinalUrl`, but is
  not included in `ClientRedirectCount`.
- `WebAccessService` uses plain HTTP and cannot execute JavaScript, so its
  `ClientRedirectCount` is always `0`.

For example, callers can reject a browser-verified suggestion that navigated
somewhere else:

```csharp
var check = await fetcher.CheckReachabilityAsync("https://test.example.com");
if (check.ClientRedirectCount > 0)
{
    // The requested URL changed through client-side navigation.
}
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
| `RedirectCount` | Number of HTTP redirects followed by the plain-HTTP checker; browser fetchers leave this at `0` |
| `ClientRedirectCount` | Number of same-host client-side URL changes observed after browser page load (`0` or `1`) |
| `FinalUrl` | URL after server-side and client-side browser navigation |
| `ProtectionType` | Detected protection type, when reported by the engine |
