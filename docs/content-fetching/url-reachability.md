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
- A URL is reachable when the final status is `2xx` or `3xx` (including 304)
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

`IBrowserSession.CheckReachabilityAsync` uses the same accepted reachability
status policy, including supported 3xx statuses and 304, while still exposing
only a boolean result. Use `IWebContentFetcher` when the final URL and client
redirect metadata are required.

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
not immediately when the initial HTTP response arrives. The browser keeps
observing the main frame for a bounded window because client-side navigation
may be scheduled after `DOMContentLoaded` or after the page first becomes
network-idle. Redirects that occur after that window cannot be observed.

The default observation window is **5 seconds** for the normal browser content
fetcher and **10 seconds** for the visible-browser fallback and browser-session
navigation. This delay is intentional: the browser must remain available long
enough to observe delayed JavaScript, meta-refresh, or SPA navigation before it
can return a final result. The waits are fully asynchronous—the implementation
awaits Playwright and `Task.Delay` operations and does not block an application
thread with `Thread.Sleep` or a synchronous wait. Cancellation can interrupt
the pending operation.

`ClientRedirectCount` is the number of observed main-frame client-side URL
changes during that window. It may be greater than `1` for multi-hop
navigation and includes cross-origin navigation when it is observed.

Browser checks also distinguish server-side and client-side redirects:

- `RedirectCount` represents HTTP redirect hops. The plain-HTTP checker tracks
  them through `WebAccessService`; browser fetchers can populate it from
  `navigation.Navigation.RedirectCount`.
- `ClientRedirectCount` represents observed browser-side URL changes after the
  initial navigation. It is `0` when none are observed and can be greater than
  `1` for multi-hop client navigation. The counters are independent; a
  client-side navigation whose destination follows HTTP redirects can increment
  both.
- `FinalUrl` is still returned when a client-side navigation crosses origins.
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
| `RedirectCount` | Number of HTTP redirect hops reported by the plain-HTTP checker or browser fetcher (`navigation.Navigation.RedirectCount`); `0` when none are observed |
| `ClientRedirectCount` | Number of observed main-frame client-side URL changes during the bounded browser observation window; can be greater than `1` |
| `FinalUrl` | URL after server-side and client-side browser navigation |
| `ProtectionType` | Detected protection type, when reported by the engine |
