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
| `Content` | Content in the representation requested by the fetch operation: plain text for `FetchAsync`, or the selected `EContentFormat` for `FetchAsAsync` |
| `ErrorMessage` | Failure reason when `Success` is `false` |
| `FinalUrl` | Browser-reported URL once page navigation and the bounded post-load observation window are complete, including observed server-side redirects and client-side navigation |

## UrlCheckResult

Outcome of a URL reachability check.

```csharp
public sealed record UrlCheckResult(
    bool Reachable,
    int? HttpStatus,
    string? ErrorMessage,
    int RedirectCount = 0,
    string? FinalUrl = null,
    string? ProtectionType = null,
    int ClientRedirectCount = 0);
```

| Property | Description |
| --- | --- |
| `Reachable` | Whether the URL loaded successfully |
| `HttpStatus` | Final HTTP status code, when available |
| `ErrorMessage` | Failure reason when not reachable |
| `RedirectCount` | Number of redirects followed |
| `FinalUrl` | URL after the reachability check completes; for browser checks, this includes observed client-side navigation |
| `ClientRedirectCount` | Number of observed main-frame client-side URL changes during the bounded browser observation window; can be greater than `1` |
| `ProtectionType` | Detected protection type, when reported by the engine |
