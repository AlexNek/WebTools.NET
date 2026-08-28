# BrowserSession

`BrowserSession` is the stateful browser capability in WebTools.NET. It accepts
an externally created `IBrowserSession`, executes caller-requested
`BrowserOperation` values, and returns a `BrowserSnapshot` after startup and
each operation.

WebTools.NET does not choose operations or contain an agent/decision loop. A
console application, workflow, or orchestration layer can use the same API.

## Construction and ownership

Create one browser session and one `BrowserSession` wrapper for each independent
workflow. The caller owns and disposes the supplied browser session; the wrapper
never creates, replaces, or disposes it.

```csharp
using WebTools.NET;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

var factory = new BrowserSessionFactory();
await using var browser = factory.Create();
await using var session = new BrowserSession(
    browser,
    new BrowserSessionOptions
    {
        MaxOperations = 50,
        MaxDuration = TimeSpan.FromMinutes(5),
        IncludeScreenshot = false
    });

var snapshot = await session.StartAsync("https://test.example.com");
```

Select `EContentFormat.MarkdownWithAbsoluteUrls` through `BrowserSessionOptions.DefaultFormat` when snapshots must be self-contained:

```csharp
await using var standaloneBrowser = factory.Create();
await using var standaloneSession = new BrowserSession(
    standaloneBrowser,
    new BrowserSessionOptions
    {
        DefaultFormat = EContentFormat.MarkdownWithAbsoluteUrls
    });

var standaloneSnapshot = await standaloneSession.StartAsync("https://test.example.com/docs/");
```

For dependency injection, resolve `IBrowserSessionFactory` and create a fresh
session for each workflow. Do not share one browser session between independent
workflows.

## Operations

`ExecuteAsync` supports these operation types:

- `Navigate` — navigate to a URL in `Value`
- `Click` — click the element identified by `ElementIndex`
- `Fill` — fill an element with `Value`
- `FillForm` — validate and fill multiple text, checkbox, or select fields
- `Select` — select an option by element index
- `Submit` — submit the form containing an element
- `ScrollDown` and `ScrollUp` — move by one configured viewport height
- `WaitFor` — wait for a CSS selector in `Value`
- `Back` — navigate back
- `Snapshot` — rebuild the current snapshot without interaction

Element indexes are ephemeral. They are re-extracted after every operation, so
the caller should use the indexes from the most recent snapshot only.

```csharp
snapshot = await session.ExecuteAsync(new BrowserOperation(
    EBrowserOperationType.Fill,
    ElementIndex: 1,
    Value: "example value"));

snapshot = await session.ExecuteAsync(new BrowserOperation(
    EBrowserOperationType.Click,
    ElementIndex: 2));
```

## BrowserSnapshot

A snapshot contains the current URL, title, formatted content, interactive
elements, observed HTTP status, an error when the operation or page failed, a
`HasMoreContent` scrolling hint, and an optional base64 screenshot.

Failures are normally reported in `BrowserSnapshot.Error` while preserving the
last usable page state. Caller cancellation remains cancellation and is not
converted into a normal operation error.

## Screenshots

Direct screenshots use the visible viewport by default. Pass
`EScreenshotScope.FullPage` to capture the full scrollable page:

```csharp
var viewportScreenshot = await session.ScreenshotAsync();
var fullPageScreenshot = await session.ScreenshotAsync(EScreenshotScope.FullPage);
```

When `BrowserSessionOptions.IncludeScreenshot` is enabled, snapshots use
`DefaultScreenshotScope`, which defaults to `EScreenshotScope.Viewport`.

## Limits and persistence

`BrowserSessionOptions` configures the maximum operation count, maximum session
duration, output format, screenshot inclusion and scope, storage-state path,
and viewport. `DefaultScreenshotScope` defaults to `EScreenshotScope.Viewport`;
set it to `EScreenshotScope.FullPage` when opt-in snapshot screenshots should
include the full scrollable page. Storage state is loaded before the first
navigation and saved by the wrapper when configured; the browser session itself
remains caller-owned.

Built-in browser sessions serialize page operations, reset, and disposal through
a lifecycle gate. If a session deadline interrupts an in-flight operation, a
reset cannot close its page or context concurrently with that operation.

See [Caller Integration](caller-integration.md) for composition patterns,
[Migration from Agent APIs](../getting-started/migration.md) for legacy-name
mappings and ownership differences, and
[Core Interfaces](../api-reference/interfaces.md) for the public contracts.
