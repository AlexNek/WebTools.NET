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
    bool headless = true)
```

Registers the browser-backed services for the selected engine:

| Service | `EBrowserEngine.Playwright` | `EBrowserEngine.CloakBrowser` | Lifetime |
| --- | --- | --- | --- |
| `IWebContentFetcher` | `PlaywrightContentFetcher` | `CloakBrowserContentFetcher` | Singleton |
| `IWebSearchProvider` | `PlaywrightSearchProvider` | `CloakBrowserSearchProvider` | Singleton |
| `IBrowserInteraction` | `PlaywrightSession` | `CloakBrowserSession` | Singleton |

Browser-based search providers receive an `ILogger<T>` from the container when
logging is registered; otherwise they run without logging.

Throws `ArgumentNullException` when `services` is null.

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
using WebTools.NET.Abstractions;

var services = new ServiceCollection();

services.AddLogging();
services.AddWebToolsCore();
services.AddBrowserServices(EBrowserEngine.Playwright, headless: true);

await using var provider = services.BuildServiceProvider();

var webAccess = provider.GetRequiredService<IWebAccessService>();
var fetcher = provider.GetRequiredService<IWebContentFetcher>();
```
