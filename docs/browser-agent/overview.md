# Browser Agent

`BrowserAgent` is a stateful browser agent that lets an LLM autonomously
navigate, interact with, and extract information from web pages across
multiple turns. It maintains a persistent browser session and exposes a
structured action vocabulary.

## Architecture

```
┌─────────────────────────────────────────────────┐
│  LLM / Orchestrator                             │
│  (decides actions based on page state)          │
└──────────────────┬──────────────────────────────┘
                   │ BrowserAction
                   ▼
┌─────────────────────────────────────────────────┐
│  BrowserAgent                                   │
│  - Maintains session state                      │
│  - Executes actions                             │
│  - Returns PageSnapshot after each action       │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│  IBrowserInteraction (PlaywrightSession)        │
│  - Navigate, Click, Fill, Scroll, etc.          │
└─────────────────────────────────────────────────┘
```

## Quick Start

```csharp
using WebTools.NET;
using WebTools.NET.Models;

// Agent owns its own browser session
await using var agent = new BrowserAgent();

var snapshot = await agent.StartAsync("https://test.example.com");

// LLM decides next action based on snapshot
var action = new BrowserAction(EBrowserActionType.Click, ElementIndex: 3);
snapshot = await agent.ExecuteAsync(action);
```

## Construction

```csharp
// Owns a PlaywrightSession internally — simplest setup
await using var agent1 = new BrowserAgent();

// With options
var options = new BrowserAgentOptions
{
    MaxActions = 100,
    MaxDuration = TimeSpan.FromMinutes(10),
    IncludeScreenshot = true,
    StorageStatePath = "./cookies.json"
};
await using var agent2 = new BrowserAgent(options);

// Wraps any IBrowserInteraction — caller owns its lifetime
await using var agent3 = new BrowserAgent(browserSession, options, logger);
```

Both constructors accept an optional `ILogger<BrowserAgent>`.

## Action Vocabulary

| Action | Purpose | Required fields |
| --- | --- | --- |
| `Navigate` | Go to a URL | `Value` = URL |
| `Click` | Click element by index | `ElementIndex` |
| `Fill` | Fill input by index | `ElementIndex`, `Value` |
| `FillForm` | Fill multiple fields at once | `Fields` (array of `FormFieldValue`) |
| `Select` | Select dropdown option | `ElementIndex`, `Value` = option text |
| `Submit` | Submit form containing element | `ElementIndex` |
| `ScrollDown` | Scroll one viewport down (lazy loading) | — |
| `ScrollUp` | Scroll one viewport up | — |
| `WaitFor` | Wait for CSS selector to appear | `Value` = selector, optional `TimeoutMs` |
| `Back` | Browser back button | — |
| `Snapshot` | Re-read page without interaction | — |

## PageSnapshot

After every action, the agent returns a `PageSnapshot`:

| Field | Description |
| --- | --- |
| `Url` | Current page URL after redirects |
| `Title` | Page title |
| `Content` | Page content formatted per chosen `EContentFormat` |
| `Elements` | Interactive elements (links, buttons, inputs, selects) |
| `Format` | Content format used |
| `StatusCode` | HTTP status of last navigation |
| `Error` | Error description on failure, null on success |
| `HasMoreContent` | True if page has more content below current scroll |
| `ScreenshotBase64` | Base64 PNG when `IncludeScreenshot` is enabled |

## Element Indexing

Elements are numbered 1..N in each snapshot. The LLM refers to them by
index. After each action, elements are re-extracted and re-indexed —
indices are ephemeral and not stable across turns.

## Error Handling

Failed actions never crash the session. The returned snapshot has `Error`
populated and the session stays alive for the next action.

```csharp
var snapshot = await agent.ExecuteAsync(
    new BrowserAction(EBrowserActionType.Click, ElementIndex: 99));

if (snapshot.Error is not null)
{
    // "Element index 99 not found"
    // Session is still alive — try a different action
}
```

## Safety Limits

| Option | Default | Description |
| --- | --- | --- |
| `MaxActions` | 50 | Maximum actions per session |
| `MaxDuration` | 5 minutes | Maximum session wall-clock time |

When a limit is reached, `ExecuteAsync` returns a snapshot with an error
message and refuses further actions.

## Cookie Persistence

Set `StorageStatePath` to persist cookies across sessions:

```csharp
var options = new BrowserAgentOptions
{
    StorageStatePath = "./browser-state.json"
};
await using var agent = new BrowserAgent(options);

// Cookies are loaded at start and saved on dispose
var snapshot = await agent.StartAsync("https://test.example.com/dashboard");
```

## Lifetime

Dispose the agent with `await using`. Disposal saves storage state (if
configured) and closes the internally owned browser session. Sessions
passed into the constructor remain your responsibility.
