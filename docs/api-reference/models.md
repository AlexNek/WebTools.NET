# Models

All result models are immutable records in the `WebTools.NET.Models`
namespace.

## SearchResult

Outcome of a web search.

```csharp
public sealed record SearchResult(
    bool Success,
    IReadOnlyList<SearchResultItem> Results,
    string? ErrorMessage);
```

| Property | Description |
| --- | --- |
| `Success` | Whether the search completed without error |
| `Results` | Matching entries; empty on failure |
| `ErrorMessage` | Failure reason when `Success` is `false` |

## SearchResultItem

One search result entry.

```csharp
public sealed record SearchResultItem(string Title, string Url, string Snippet);
```

## WebContent

Outcome of a page fetch.

```csharp
public sealed record WebContent(
    bool Success,
    string Content,
    string? ErrorMessage,
    string FinalUrl);
```

| Property | Description |
| --- | --- |
| `Success` | Whether the fetch completed successfully |
| `Content` | Page content as plain text (HTML stripped) |
| `ErrorMessage` | Failure reason when `Success` is `false` |
| `FinalUrl` | URL of the page after redirects |

## UrlCheckResult

Outcome of a URL reachability check.

```csharp
public sealed record UrlCheckResult(
    bool Reachable,
    int? HttpStatus,
    string? ErrorMessage,
    int RedirectCount = 0,
    string? FinalUrl = null,
    string? ProtectionType = null);
```

| Property | Description |
| --- | --- |
| `Reachable` | Whether the URL loaded successfully |
| `HttpStatus` | Final HTTP status code, when available |
| `ErrorMessage` | Failure reason when not reachable |
| `RedirectCount` | Number of redirects followed |
| `FinalUrl` | URL after redirects |
| `ProtectionType` | Detected protection type, when reported by the engine |
