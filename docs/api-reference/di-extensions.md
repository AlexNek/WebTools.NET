# DI Extensions

Extension methods on `IServiceCollection` in the
`Microsoft.Extensions.DependencyInjection` namespace (class
`WebToolsServiceCollectionExtensions`).

## AddWebToolsCore

```csharp
public static IServiceCollection AddWebToolsCore(this IServiceCollection services)
```

Registers the HTTP-only core services:

| Service | Implementation | Lifetime |
| --- | --- | --- |
| `IWebAccessService` | `WebAccessService` | Singleton |

Throws `ArgumentNullException` when `services` is null.

## AddBrowserServices

```csharp
public static IServiceCollection AddBrowserServices(
    this IServiceCollection services,
    EBrowserEngine engine = EBrowserEngine.Playwright,
    bool headless = true,
    BrowserSessionOptions? browserSessionOptions = null)
```

Registers the browser-backed services for the selected engine:

| Service | `EBrowserEngine.Playwright` | `EBrowserEngine.CloakBrowser` | Lifetime |
| --- | --- | --- | --- |
| `IWebContentFetcher` | `PlaywrightContentFetcher` | `CloakBrowserContentFetcher` | Singleton |
| `IWebSearchProvider` | `PlaywrightSearchProvider` | `CloakBrowserSearchProvider` | Singleton |
| `IBrowserInteraction` | `PlaywrightSession` | `CloakBrowserSession` | Singleton |
| `IBrowserSessionFactory` | `BrowserSessionFactory` | `BrowserSessionFactory` | Singleton |

`BrowserSession` is intentionally not registered directly because it requires
an explicitly supplied external browser session. Resolve
`IBrowserSessionFactory`, create one fresh session per workflow, pass that
session to `BrowserSession`, and dispose both explicitly.
`browserSessionOptions` configures factory-created browser sessions and session
limits.

For existing applications, obsolete overloads accepting `BrowserAgentOptions`
remain available. The legacy `IBrowserAgentSessionFactory` and
`IBrowserAgentInteraction` registrations are aliases for the same current
factory/session instances; they do not register `BrowserAgent` or a shared
`BrowserSession`. Migrate the options object and contracts at your own pace:

```csharp
services.AddBrowserServices(new BrowserAgentOptions
{
    SessionOptions = new BrowserSessionOptions { ViewportHeight = 720 }
});
var legacyFactory = provider.GetRequiredService<IBrowserAgentSessionFactory>();
```

Use `BrowserSessionOptions`, `IBrowserSessionFactory`, and `IBrowserSession` in
new code. The old `BrowserAgent`, action/model, and service wrapper names are
also retained as obsolete forwarding types; see the migration section in the
README.

Throws `ArgumentNullException` when `services` is null.

The legacy options overload is retained for existing applications:

```csharp
services.AddBrowserServices(new BrowserAgentOptions
{
    SessionOptions = new BrowserSessionOptions { ViewportHeight = 720 }
});
```

It forwards to the current `BrowserSessionOptions` registration. The obsolete
`IBrowserAgentSessionFactory` and `IBrowserAgentInteraction` contracts resolve
the same current factory and browser-session implementations as the preferred
session contracts. New code should use `BrowserSessionOptions` directly.

## Overriding Registrations

Both methods use `TryAdd` semantics: an existing registration for the same
service type is kept. To override, register your implementation first:

```csharp
services.AddSingleton<IWebSearchProvider, MySearchProvider>();
services.AddBrowserServices();   // IWebSearchProvider stays MySearchProvider
```

## Full Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using WebTools.NET;
using WebTools.NET.Abstractions;

var services = new ServiceCollection();

services.AddLogging();
services.AddWebToolsCore();
services.AddBrowserServices(EBrowserEngine.Playwright, headless: true);

await using var provider = services.BuildServiceProvider();

var webAccess = provider.GetRequiredService<IWebAccessService>();
var fetcher = provider.GetRequiredService<IWebContentFetcher>();
var sessionFactory = provider.GetRequiredService<IBrowserSessionFactory>();
await using var browser = sessionFactory.Create();
await using var session = new BrowserSession(browser);
var snapshot = await session.StartAsync("https://test.example.com");
```
