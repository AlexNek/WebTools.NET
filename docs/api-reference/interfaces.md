# Core Interfaces

All interfaces live in the `WebTools.NET.Abstractions` namespace.

## IWebAccessService

Plain-HTTP URL reachability checking.

```csharp
public interface IWebAccessService
{
    Task<UrlCheckResult> CheckReachabilityAsync(string url, CancellationToken ct = default);
}
```

Implementation: `WebAccessService`.

## IWebSearchProvider

Single web search execution.

```csharp
public interface IWebSearchProvider
{
    Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default);
}
```

Implementations: `DuckDuckGoSearchProvider`, `PlaywrightSearchProvider`,
`CloakBrowserSearchProvider`.

## IWebContentFetcher

Browser-based page content retrieval. Extends `IAsyncDisposable`.

```csharp
public interface IWebContentFetcher : IAsyncDisposable
{
    Task<UrlCheckResult> CheckReachabilityAsync(string url, CancellationToken ct = default);

    Task<WebContent> FetchAsync(string url, CancellationToken ct = default);
}
```

Implementations: `PlaywrightContentFetcher`, `CloakBrowserContentFetcher`.

## IBrowserInteraction

Low-level browser session control. Extends `IAsyncDisposable`.

```csharp
public interface IBrowserInteraction : IAsyncDisposable
{
    Task<bool> CheckReachabilityAsync(string url, CancellationToken ct = default);

    Task ClickAsync(string selector, CancellationToken ct = default);

    Task FillAsync(string selector, string value, CancellationToken ct = default);

    Task<string> GetContentAsync(CancellationToken ct = default);

    Task<string> GetCurrentUrlAsync(CancellationToken ct = default);

    Task<string> GetHtmlAsync(CancellationToken ct = default);

    Task NavigateAsync(string url, CancellationToken ct = default);
}
```

Implementations: `PlaywrightSession`, `CloakBrowserSession`.

## IGeoRegionProvider

Region detection.

```csharp
public interface IGeoRegionProvider
{
    Task<string> DetectRegionAsync(CancellationToken ct = default);
}
```

Implementation: `GeoRegionAgent`. Returns one of `us`, `eu`, `china`, `intl`.

## EBrowserEngine

Engine selector for DI registration.

```csharp
public enum EBrowserEngine
{
    Playwright,
    CloakBrowser
}
```
