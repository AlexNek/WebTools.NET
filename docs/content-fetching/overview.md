# Content Fetching Overview

Content fetching retrieves the rendered content of a web page through a
headless browser, behind the `IWebContentFetcher` abstraction.

## Fetching a Page

```csharp
var fetcher = provider.GetRequiredService<IWebContentFetcher>();

var content = await fetcher.FetchAsync("https://test.example.com");
if (!content.Success)
{
    logger.LogWarning("Fetch failed: {Error}", content.ErrorMessage);
    return;
}

Console.WriteLine(content.FinalUrl);   // URL after any redirects
Console.WriteLine(content.Content);    // page content as plain text
```

The returned `WebContent` record:

| Property | Description |
| --- | --- |
| `Success` | Whether the fetch completed successfully |
| `Content` | Page content extracted from the rendered body (HTML stripped) |
| `ErrorMessage` | Failure reason when `Success` is `false` |
| `FinalUrl` | The URL of the page after redirects |

## Limiting Content Length

By default `FetchAsync` returns the full page content without truncation.
Pass `maxContentLength` to limit the output to a specific number of characters:

```csharp
// Get at most 8000 characters of content
var content = await fetcher.FetchAsync("https://test.example.com", maxContentLength: 8000);
```

The returned content is cut to exactly the specified character count — no
truncation indicator is appended. If you need to signal to downstream
consumers that content was trimmed, append your own suffix after the call.

Passing `null` (the default) returns everything. Passing a value ≤ 0 throws
`ArgumentOutOfRangeException`.

## Content Format

Use `FetchAsAsync` to control the output format:

```csharp
// Get page content as Markdown (preserves tables, headings, links, images)
var md = await fetcher.FetchAsAsync("https://test.example.com", EContentFormat.Markdown);

// Get raw HTML with noise removed (scripts, styles, nav, footer stripped)
var html = await fetcher.FetchAsAsync("https://test.example.com", EContentFormat.Html);

// Plain text (same as FetchAsync)
var text = await fetcher.FetchAsAsync("https://test.example.com", EContentFormat.PlainText);
```

| Format | Output | Best for |
| --- | --- | --- |
| `PlainText` | Stripped text, whitespace collapsed | Token-efficient LLM input |
| `Markdown` | GitHub-flavored Markdown with tables, headings, links, images | LLMs that benefit from structure |
| `Html` | Body HTML with noise tags removed | Downstream HTML processing |

`maxContentLength` works with all formats — it applies after conversion:

```csharp
var md = await fetcher.FetchAsAsync(
    "https://test.example.com",
    EContentFormat.Markdown,
    maxContentLength: 4000);
```

## Sanitization Level

By default, `FetchAsAsync` strips navigation noise (script, style, nav, footer,
header) before conversion. Use `ESanitizeLevel` to control this:

```csharp
// Default: strip all noise — best for reading article content
var article = await fetcher.FetchAsAsync(url, EContentFormat.Markdown);

// Minimal: keep nav/header/footer — best for page discovery and link finding
var page = await fetcher.FetchAsAsync(
    url,
    EContentFormat.Markdown,
    sanitizeLevel: ESanitizeLevel.Minimal);

// None: no sanitization — raw body HTML or full Markdown conversion
var raw = await fetcher.FetchAsAsync(url, EContentFormat.Html, sanitizeLevel: ESanitizeLevel.None);
```

| Level | Removes | Best for |
| --- | --- | --- |
| `Strict` (default) | script, style, nav, footer, header | Reading main content |
| `Minimal` | script, style only | Page discovery, finding navigation links |
| `None` | Nothing | Full page structure needed |

## Engine Implementations

| Engine | Implementation |
| --- | --- |
| `EBrowserEngine.Playwright` | `PlaywrightContentFetcher` |
| `EBrowserEngine.CloakBrowser` | `CloakBrowserContentFetcher` |

Both accept a `headless` flag in their constructor (`true` by default) and
are registered for you by `AddBrowserServices()`. Because content is read
from the fully rendered page, JavaScript-heavy pages work the same as static
ones. Both implementations also use the shared browser reachability pipeline:
reachability waits for post-load navigation to settle, captures the final
browser URL after JavaScript navigation, and reports same-host client-side
redirects through `ClientRedirectCount`. The two engines are alternatives
selected by DI, not an automatic fallback chain.

!!! tip
    Use the CloakBrowser engine when target pages block plain Playwright
    sessions — see [Browser Engines](../concepts/browser-engines.md).

## Reachability Before Fetching

Both fetchers also implement `CheckReachabilityAsync`, which navigates to the
URL and reports whether the page loaded without downloading and parsing its
full content:

```csharp
var check = await fetcher.CheckReachabilityAsync("https://test.example.com");
if (!check.Reachable)
{
    // Skip the expensive fetch
}
```

See [URL Reachability](url-reachability.md) for the full contract.

## Lifetime

Fetchers implement `IAsyncDisposable`. When resolved from DI as singletons
they are disposed together with the container; when constructed manually,
dispose them with `await using`.
