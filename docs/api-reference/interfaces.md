# Core Interfaces

The core WebTools.NET interfaces live in the `WebTools.NET.Abstractions` namespace. The HTML content-analysis interface belongs to the companion package and is declared in `WebTools.NET.ContentAnalysis.Abstractions`.

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

    Task<WebContent> FetchAsync(
        string url,
        int? maxContentLength = null,
        CancellationToken ct = default);

    Task<WebContent> FetchAsAsync(
        string url,
        EContentFormat format,
        int? maxContentLength = null,
        ESanitizeLevel sanitizeLevel = ESanitizeLevel.Strict,
        CancellationToken ct = default);
}
```

`FetchAsync` returns plain text. Here, “final URL” means the browser-reported
URL once navigation and the browser's bounded post-load observation window are
complete. It includes observed server-side redirects and client-side navigation.
If no redirect or client-side navigation occurs, it is the originally requested
URL.

`FetchAsAsync` supports `PlainText`, `Markdown`, `MarkdownWithAbsoluteUrls`, and
`Html`. In `MarkdownWithAbsoluteUrls`, relative `href` and `src` values from the
fetched page are converted to absolute URLs using that final URL as the base.

Implementations: `PlaywrightContentFetcher`, `CloakBrowserContentFetcher`.

## IHtmlContentAnalyzer

Browser-independent analysis of a complete HTML document. The implementation is provided by the `WebTools.NET.ContentAnalysis` companion package and is registered by `AddWebToolsCore()`.

```csharp
public interface IHtmlContentAnalyzer
{
    HtmlAnalysisResult Analyze(
        string html,
        HtmlAnalysisOptions? options = null,
        Uri? sourceUri = null);
}
```

Use `IBrowserContent.GetHtmlAsync()` when analyzing the complete current browser document. The analyzer does not create or own browser resources.

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

## IBrowserSession

Composite browser-session capabilities used by `BrowserSession`. Extends
`IBrowserInteraction` so low-level browser consumers remain compatible.

```csharp
public interface IBrowserSession : IBrowserInteraction
{
    Task<IReadOnlyList<InteractiveElement>> GetInteractiveElementsAsync(CancellationToken ct = default);
    Task GoBackAsync(CancellationToken ct = default);
    Task<bool> IsCheckedAsync(string selector, CancellationToken ct = default);
    Task<int?> GetLastNavigationStatusAsync(CancellationToken ct = default);
    Task<bool> HasMoreContentAsync(CancellationToken ct = default);
    Task LoadStorageStateAsync(string path, CancellationToken ct = default);
    Task SaveStorageStateAsync(string path, CancellationToken ct = default);
    Task<string> ScreenshotAsync(EScreenshotScope scope = EScreenshotScope.Viewport, CancellationToken ct = default);
    Task ScrollAsync(int deltaY, CancellationToken ct = default);
    Task SelectOptionAsync(string selector, string value, CancellationToken ct = default);
    Task SubmitFormAsync(string selector, CancellationToken ct = default);
    Task WaitForSelectorAsync(string selector, int timeoutMs, CancellationToken ct = default);
}
```

The scope-first overload keeps `ScreenshotAsync()` on the default viewport and
allows direct full-page capture. If you pass a cancellation token, use the
named argument because positional `ScreenshotAsync(cancellationToken)` is no
longer supported:

```csharp
var screenshot = await session.ScreenshotAsync(ct: cancellationToken);
```

Implementations: `PlaywrightSession`, `CloakBrowserSession`.

The composite is intentionally split into smaller capability interfaces for consumers
that do not need the full session surface: `IBrowserElementExtractor`,
`IBrowserHistoryNavigation`, `IBrowserFormInteraction`, `IBrowserNavigationStatus`,
`IBrowserSessionStorage`, `IBrowserScreenshot`, `IBrowserViewport`, and
`IBrowserPageWaiter`.

## IBrowserSessionFactory

Creates a fresh, unstarted browser session for each independent workflow. The
caller passes the returned session to `BrowserSession` and owns its lifetime.

```csharp
public interface IBrowserSessionFactory
{
    IBrowserSession Create();
}
```

`BrowserSessionFactory` selects `PlaywrightSession` or `CloakBrowserSession` and
never caches the returned session.

## Compatibility contracts

The former browser-agent contracts remain available as obsolete compatibility
shims so existing applications can migrate without an immediate source break:

| Legacy contract | Preferred contract |
| --- | --- |
| `IBrowserAgentInteraction` | `IBrowserSession` |
| `IBrowserAgentSessionFactory` | `IBrowserSessionFactory` |

`IBrowserSession` inherits the legacy composite capability surface, so built-in
sessions satisfy both contracts. `IBrowserAgentSessionFactory` returns the same
current session implementations through the legacy interface. Prefer the
session names for new code.

## IBrowserSessionState

Optional lifecycle state exposed by built-in sessions:

```csharp
public interface IBrowserSessionState
{
    bool IsPageReady { get; }
}
```

## BrowserSessionOptions

`BrowserSessionOptions` contains settings for both the engine session and the
orchestration wrapper. `ViewportWidth` and `ViewportHeight` are engine-session
options that must reach `PlaywrightSession` or `CloakBrowserSession` (directly or
through `IBrowserSessionFactory`). `MaxOperations`, `MaxDuration`,
`DefaultFormat`, `IncludeScreenshot`, and `DefaultScreenshotScope` are
orchestration options consumed by `BrowserSession`; pass the options object when
constructing that wrapper. `DefaultScreenshotScope` defaults to `Viewport` and
controls screenshots included in snapshots. `StorageStatePath` is used by both
layers when storage persistence is enabled. The default viewport is 1920×1080.

## IGeoRegionProvider

Region detection.

```csharp
public interface IGeoRegionProvider
{
    Task<string> DetectRegionAsync(CancellationToken ct = default);
}
```

Implementation: `GeoRegionService`. Returns one of `us`, `eu`, `china`, `intl`.

## EBrowserEngine

Engine selector for DI registration.

```csharp
public enum EBrowserEngine
{
    Playwright,
    CloakBrowser
}
```
