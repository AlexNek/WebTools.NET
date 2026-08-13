# WebSearchAgent

`WebSearchAgent` wraps any `IWebSearchProvider` and adds automatic fallback
query generation when a search returns no results.

## Construction

```csharp
// Owns a PlaywrightSearchProvider internally
await using var agent1 = new WebSearchAgent();

// Wraps any provider; caller owns its lifetime
using var ddg = new DuckDuckGoSearchProvider();
await using var agent2 = new WebSearchAgent(ddg);
```

Both constructors also accept an optional `ILogger<WebSearchAgent>`.

!!! note
    `DisposeAsync` only disposes the provider when the agent created it
    itself. Providers passed into the constructor remain your responsibility.

## Searching

```csharp
var result = await agent.SearchAsync(
    "weather API",
    maxResults: 10,
    ct: cancellationToken);
```

| Parameter | Default | Description |
| --- | --- | --- |
| `query` | — | The search query |
| `maxResults` | `10` | Maximum number of results to return |
| `ct` | `default` | Cancellation token |

## Fallback Query Strategy

When the initial query produces a failed or empty result, the agent retries
with generated variations:

| Original query | Fallback attempts (in order) |
| --- | --- |
| Contains ` API` | Query without ` API`, then query + ` official site` |
| Anything else | Query + ` official site` |

The first fallback that yields results wins. If all attempts fail, the last
`SearchResult` is returned unchanged — callers should still check `Success`.

```csharp
var result = await agent.SearchAsync("canteen menu API");
// Internally: "canteen menu API" -> "canteen menu" -> "canteen menu official site"

if (!result.Success)
{
    // All attempts failed
}
```
