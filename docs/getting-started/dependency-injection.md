# Dependency Injection

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

Registers the browser-backed services for the selected engine:

```csharp
using WebTools.NET.Abstractions;

services.AddBrowserServices(EBrowserEngine.Playwright, headless: true);
```

| Registered service | Playwright implementation | CloakBrowser implementation |
| --- | --- | --- |
| `IWebContentFetcher` | `PlaywrightContentFetcher` | `CloakBrowserContentFetcher` |
| `IWebSearchProvider` | `PlaywrightSearchProvider` | `CloakBrowserSearchProvider` |
| `IBrowserInteraction` | `PlaywrightSession` | `CloakBrowserSession` |

### Parameters

| Parameter | Default | Description |
| --- | --- | --- |
| `engine` | `EBrowserEngine.Playwright` | Selects the browser engine (see [Browser Engines](../concepts/browser-engines.md)) |
| `headless` | `true` | Runs the browser headlessly; set `false` to watch the session for debugging |

## Typical Composition

```csharp
using Microsoft.Extensions.DependencyInjection;
using WebTools.NET.Abstractions;

var services = new ServiceCollection();

services.AddLogging();
services.AddWebToolsCore();
services.AddBrowserServices(EBrowserEngine.Playwright);

var provider = services.BuildServiceProvider();
```

## Manual Construction Without DI

All agents also support direct construction, which is useful in scripts and
tests. A parameterless constructor creates and owns a default Playwright-based
dependency; passing an explicit dependency makes you responsible for its
lifetime:

```csharp
using WebTools.NET;
using WebTools.NET.Search;

// Owns its PlaywrightSearchProvider internally
await using var agent1 = new WebSearchAgent();

// You own the provider's lifetime
using var ddg = new DuckDuckGoSearchProvider();
await using var agent2 = new WebSearchAgent(ddg);
```

!!! note
    All registrations use `TryAdd*` semantics, so you can register your own
    implementation of any abstraction before calling the extensions and it
    will win.
