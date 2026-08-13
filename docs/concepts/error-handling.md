# Error Handling

WebTools.NET follows a result-object pattern: operations report failure
through the returned model instead of throwing exceptions. This keeps agent
pipelines free of try/catch noise and makes fallback logic explicit.

## Result Objects

| Model | Success flag | Error field |
| --- | --- | --- |
| `SearchResult` | `Success` | `ErrorMessage` |
| `WebContent` | `Success` | `ErrorMessage` |
| `UrlCheckResult` | `Reachable` | `ErrorMessage` |

## Handling a Failed Operation

```csharp
var content = await fetcher.FetchAsync("https://test.example.com");
if (!content.Success)
{
    logger.LogWarning("Fetch failed: {Error}", content.ErrorMessage);
    return;
}

// content.Content is safe to use here
```

## Where Exceptions Can Still Occur

Result objects cover operational failures (timeouts, HTTP errors, unreachable
hosts, bot blocks). Exceptions are still thrown for programming errors:

- `ArgumentNullException` for null dependencies or service collections
- `UriFormatException` when a caller passes a malformed URL to lower-level
  APIs such as link extraction

## Defensive Agent Behavior

The agents add their own resilience on top of result objects:

- `WebSearchAgent` retries with generated fallback queries when a search
  returns no results (see [WebSearchAgent](../search/web-search-agent.md))
- `WebNavigationAgent` returns an empty list instead of throwing when
  navigation or link extraction fails
- `GeoRegionAgent` falls back to the system locale when the Geo-IP lookup
  fails (see [Geo-awareness](../navigation/geo-region.md))

## Logging

Components accept an optional `ILogger<T>` and emit diagnostic information at
`Debug` level. Enable debug logging for `WebTools.NET` types to trace
navigation, link extraction, and fallback decisions.
