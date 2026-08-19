# WebSearchService

`WebSearchService` wraps a caller-supplied `IWebSearchProvider` and adds
automatic fallback query generation when a search returns no results. It does
not create or dispose the provider.

## Migrating from `WebSearchAgent`

`WebSearchAgent` remains available as an obsolete forwarding wrapper. Prefer
`WebSearchService` for new code. The injected legacy constructor leaves the
supplied provider caller-owned, while the parameterless legacy constructor
retains its historical internally created browser provider. See the
[Migration from Agent APIs](../getting-started/migration.md) guide for the
complete mapping.

## Construction

```csharp
// The caller creates and owns the provider.
using var ddg = new DuckDuckGoSearchProvider();
var search = new WebSearchService(ddg);
```

The constructor also accepts an optional `ILogger<WebSearchService>`.

## Searching

```csharp
var result = await search.SearchAsync(
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

When the initial query produces a failed or empty result, the service retries
with generated variations:

| Original query | Fallback attempts (in order) |
| --- | --- |
| Contains ` API` | Query without ` API`, then query + ` official site` |
| Anything else | Query + ` official site` |

The first fallback that yields results wins. If all attempts fail, the last
`SearchResult` is returned unchanged — callers should still check `Success`.

```csharp
var result = await search.SearchAsync("example API query");
// Internally: "example API query" -> "example query" -> "example API query official site"

if (!result.Success)
{
    // All attempts failed
}
```
