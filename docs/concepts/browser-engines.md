# Browser Engines

All browser-backed features in WebTools.NET go through a small set of
capabilities, with two engine choices selected via the `EBrowserEngine` enum.

## Engine Options

| Engine | Session | When to use |
| --- | --- | --- |
| `EBrowserEngine.Playwright` (default) | `PlaywrightSession` | General-purpose automation on Chromium via Microsoft Playwright |
| `EBrowserEngine.CloakBrowser` | `CloakBrowserSession` | Pages with bot detection that block plain Playwright sessions |

## Selecting an Engine

Engine selection happens at DI registration time:

```csharp
using WebTools.NET.Abstractions;

services.AddBrowserServices(EBrowserEngine.CloakBrowser, headless: true);
```

The chosen engine determines the implementations of `IWebContentFetcher`,
`IWebSearchProvider`, and the low-level `IBrowserInteraction` service. Resolve
`IBrowserSessionFactory` and call `Create()` once per independent workflow when
using `BrowserSession`.

## What CloakBrowser Adds

`CloakBrowserSession` launches the browser through CloakBrowser's
`CloakLauncher` and injects stealth scripts into pages. The stealth layer
addresses common bot-detection vectors:

- `navigator.webdriver` is masked
- Chrome automation extension surfaces (`window.chrome`) are reconstructed
- WebGL vendor/renderer values are spoofed
- Plugin list, languages, hardware concurrency, and screen properties report
  realistic values
- `Function.prototype.toString` is patched to hide the overrides

In headless mode the full stealth script set is applied; in headed mode a
minimal script set is used.

## Timeouts

Both sessions use fixed internal timeouts tuned for automation scenarios:

| Operation | Timeout |
| --- | --- |
| Click | 5 s |
| Navigate (network idle) | 15 s |
| Network idle wait after actions | 10 s |
| Reachability check | 10 s |

## Sessions Are Lazy and Explicitly Owned

Browser sessions are created lazily on first use and reused for subsequent
calls on the same instance. The low-level `IBrowserInteraction` registration
remains a singleton for compatibility, but it is not a browser-session factory.
`IBrowserSessionFactory` is stateless and returns a fresh session on every
`Create()` call. Pass that session to `BrowserSession`, and dispose the supplied
session explicitly after the wrapper; use a separate factory-created session for
each concurrent or unrelated workflow.
