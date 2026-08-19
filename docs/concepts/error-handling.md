# Error Handling

WebTools.NET follows a result-object pattern: operations report failure
through the returned model instead of throwing exceptions. This keeps caller
pipelines free of unnecessary try/catch noise and makes fallback logic explicit.

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

## Service Behavior

The services add focused resilience on top of result objects:

- `WebSearchService` retries with generated fallback queries when a search
  returns no results (see [WebSearchService](../search/web-search-service.md))
- `WebNavigationService` returns an empty list instead of throwing when
  navigation or link extraction fails
- `GeoRegionService` falls back to the system locale when the Geo-IP lookup
  fails (see [Geo-awareness](../navigation/geo-region.md))
- `BrowserSession` returns recoverable browser and page failures through
  `BrowserSnapshot.Error`; caller cancellation is propagated

## Logging

Components accept an optional `ILogger<T>` and emit diagnostic information at
`Debug` level. Enable debug logging for `WebTools.NET` types to trace
navigation, link extraction, and fallback decisions.
