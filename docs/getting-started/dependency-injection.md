WebTools.NET ships two extension methods on `IServiceCollection` (namespace
`Microsoft.Extensions.DependencyInjection`).

## AddWebToolsCore

Registers the HTTP-only core services:

```csharp
services.AddWebToolsCore();
```

| Registered service | Implementation |
| --- | --- |
| `IWebAccessService` | `WebAccessService` |

No browser is required for this registration.

## AddBrowserServices

Registers browser-backed services for the selected engine:

```csharp
using WebTools.NET.Abstractions;
using WebTools.NET.Models;

services.AddBrowserServices(
    EBrowserEngine.Playwright,
    headless: true,
    browserSessionOptions: new BrowserSessionOptions
    {
        MaxOperations = 100,
        ViewportHeight = 720
    });
```

| Registered service | Playwright implementation | CloakBrowser implementation |
| --- | --- | --- |
| `IWebContentFetcher` | `PlaywrightContentFetcher` | `CloakBrowserContentFetcher` |
| `IWebSearchProvider` | `PlaywrightSearchProvider` | `CloakBrowserSearchProvider` |
| `IBrowserInteraction` | `PlaywrightSession` | `CloakBrowserSession` |
| `IBrowserSessionFactory` | `BrowserSessionFactory` | `BrowserSessionFactory` |

### Parameters

| Parameter | Default | Description |
| --- | --- | --- |
| `engine` | `EBrowserEngine.Playwright` | Selects the browser engine |
| `headless` | `true` | Runs the browser headlessly; set `false` for debugging |
| `browserSessionOptions` | `null` | Optional operation limits, storage persistence, screenshots, and viewport settings |

`BrowserSession` is not registered directly because it requires an explicitly
created external browser session. Resolve `IBrowserSessionFactory` and create
one fresh browser session for each independent workflow:

```csharp
var factory = provider.GetRequiredService<IBrowserSessionFactory>();
await using var browser = factory.Create();
await using var session = new BrowserSession(browser);
```

`BrowserSession` is non-owning. The caller owns and disposes the supplied
browser session. Declaring the browser before the wrapper ensures the wrapper
is disposed first.

### Migrating existing registrations

The former registration shape remains available as an obsolete overload, so an
existing application can migrate independently of the rest of its code:

```csharp
services.AddBrowserServices(new BrowserAgentOptions
{
    MaxActions = 50,
    SessionOptions = new BrowserSessionOptions { ViewportHeight = 720 }
});

var legacyFactory = provider.GetRequiredService<IBrowserAgentSessionFactory>();
```

`IBrowserAgentSessionFactory` and `IBrowserAgentInteraction` resolve the same
current factory and browser-session implementations as their preferred
`IBrowserSessionFactory` and `IBrowserSession` contracts. New code should use
`BrowserSessionOptions` and the session contracts directly. `BrowserSession` is
still not registered automatically; the caller owns each created browser
session and passes it to the non-owning `BrowserSession` wrapper.

## Manual construction without DI

Create the concrete browser session explicitly when DI is not used:

```csharp
using WebTools.NET;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

await using var browser = new PlaywrightSession(
    options: new BrowserSessionOptions { ViewportHeight = 720 });
await using var session = new BrowserSession(browser);
var snapshot = await session.StartAsync("https://test.example.com");
```

Other services also accept externally supplied providers or clients:

```csharp
using WebTools.NET;
using WebTools.NET.Search;

using var ddg = new DuckDuckGoSearchProvider();
var search = new WebSearchService(ddg);
var result = await search.SearchAsync("dotnet web scraping");
```

!!! note
    All registrations use `TryAdd*` semantics, so you can register your own
    implementation of any abstraction before calling the extensions and it
    will win.
