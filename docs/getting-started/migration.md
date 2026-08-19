# Migrating from the Agent-Oriented APIs

The session-oriented APIs are the preferred names for new code. The former
agent-oriented contracts remain available as obsolete compatibility shims, so
an existing application can migrate incrementally instead of changing every
caller at once.

## API replacement table

| Previous API | Preferred API | What changes |
| --- | --- | --- |
| `BrowserAgent` | `BrowserSession` | The caller chooses operations; the library does not contain an agent or decision loop. |
| `IBrowserAgentInteraction` | `IBrowserSession` | The composite browser capability uses session terminology. |
| `IBrowserAgentSessionFactory` | `IBrowserSessionFactory` | The factory creates a fresh external session for each workflow. |
| `BrowserAgentSessionFactory` | `BrowserSessionFactory` | Same factory behavior with the preferred name. |
| `BrowserAction` | `BrowserOperation` | Requests use operation terminology. |
| `EBrowserActionType` | `EBrowserOperationType` | Action enum members map explicitly to operation enum members. |
| `PageSnapshot` | `BrowserSnapshot` | Results describe current browser state rather than an agent action. |
| `BrowserAgentOptions` | `BrowserSessionOptions` | Limits, format, screenshots, storage, and viewport settings use one session options type. |
| `WebSearchAgent` | `WebSearchService` | The preferred service receives and does not own an `IWebSearchProvider`. |
| `WebNavigationAgent` | `WebNavigationService` | The preferred service receives and does not own an `IBrowserInteraction`. |
| `GeoRegionAgent` | `GeoRegionService` | The preferred service supports an injected, caller-owned `HttpClient`. |

## Browser-session ownership

Create one browser session and one `BrowserSession` wrapper per independent
workflow:

```csharp
var factory = provider.GetRequiredService<IBrowserSessionFactory>();
await using var browser = factory.Create();
await using var session = new BrowserSession(browser);

var snapshot = await session.StartAsync("https://test.example.com");
```

`BrowserSession` never creates, replaces, or disposes the supplied browser
session. The caller owns both the factory-created session and its lifetime.
The obsolete `BrowserAgent` forwards to the same session implementation and
keeps this external ownership boundary.

## Service ownership

The preferred search and navigation services require caller-supplied
dependencies:

```csharp
using var provider = new DuckDuckGoSearchProvider();
var search = new WebSearchService(provider);

await using var browser = new PlaywrightSession();
var navigation = new WebNavigationService(browser);
```

The injected legacy `WebSearchAgent` and `WebNavigationAgent` constructors also
leave supplied dependencies caller-owned. Their parameterless constructors are
compatibility-only and retain their historical behavior by creating internal
browser dependencies; dispose those wrappers when finished. `GeoRegionService`
and `GeoRegionAgent` similarly leave an injected `HttpClient` caller-owned.

## Dependency injection migration

Both generations of DI contracts are available during the compatibility period:

```csharp
services.AddBrowserServices(
    browserSessionOptions: new BrowserSessionOptions
    {
        MaxOperations = 50,
        ViewportHeight = 720
    });

var factory = provider.GetRequiredService<IBrowserSessionFactory>();
var legacyFactory = provider.GetRequiredService<IBrowserAgentSessionFactory>();
```

The legacy and preferred factory contracts resolve the same current factory
implementation. The corresponding browser interaction contracts resolve the
same current browser implementation. `BrowserSession` is not registered as a
shared service because each workflow needs its own externally owned session.

Existing calls using `BrowserAgentOptions` remain supported through obsolete
`AddBrowserServices` overloads. Migrate that options object to
`BrowserSessionOptions` when convenient.

## Suggested migration order

1. Replace factory and browser contracts with `IBrowserSessionFactory` and
   `IBrowserSession`.
2. Replace `BrowserAgentOptions` with `BrowserSessionOptions`.
3. Replace `BrowserAction` and `EBrowserActionType` with
   `BrowserOperation` and `EBrowserOperationType`.
4. Replace `PageSnapshot` with `BrowserSnapshot`.
5. Replace the service wrappers with `WebSearchService`,
   `WebNavigationService`, and `GeoRegionService`.
6. Remove obsolete warnings after all callers have migrated.

See [BrowserSession](../browser-session/overview.md),
[Dependency Injection](dependency-injection.md), and the
[API reference](../api-reference/interfaces.md) for the preferred APIs.
