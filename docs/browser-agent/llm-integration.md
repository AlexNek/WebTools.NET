# LLM Integration

This guide shows how to wire `BrowserAgent` into an LLM tool-calling loop.

## Basic Loop Pattern

```csharp
using WebTools.NET;
using WebTools.NET.Models;

var options = new BrowserAgentOptions
{
    DefaultFormat = EContentFormat.Markdown,
    IncludeScreenshot = false,
    MaxActions = 50
};

await using var agent = new BrowserAgent(options);
var snapshot = await agent.StartAsync("https://test.example.com/login");

while (true)
{
    // Present snapshot to LLM
    var action = await llm.DecideNextActionAsync(snapshot);

    if (action is null)
        break; // LLM says "done"

    snapshot = await agent.ExecuteAsync(action);

    if (snapshot.Error is not null)
    {
        // Optionally let the LLM see the error and decide recovery
        continue;
    }
}
```

## What the LLM Sees

Each `PageSnapshot` gives the LLM:

1. **Content** — the page body in Markdown (or PlainText/Html, per config).
   Formatted for token efficiency while preserving structure.

2. **Elements** — a numbered list of interactive elements:
   ```
   [1] a: "Sign In" → /login
   [2] input[text]: placeholder="Email" name="email"
   [3] input[password]: placeholder="Password" name="password"
   [4] button: "Submit"
   ```

3. **Metadata** — URL, title, errors, scroll state.

## Login Flow Example

```csharp
// Step 1: Navigate to login
var snapshot = await agent.StartAsync("https://test.example.com/login");

// Step 2: Fill form (compound action)
snapshot = await agent.ExecuteAsync(new BrowserAction(
    EBrowserActionType.FillForm,
    Fields: [
        new FormFieldValue(2, "user@test.example.com"),
        new FormFieldValue(3, "test-password-123")
    ]));

// Step 3: Submit
snapshot = await agent.ExecuteAsync(new BrowserAction(
    EBrowserActionType.Click, ElementIndex: 4));

// Step 4: Verify — snapshot.Url should be /dashboard
```

## Scroll for Lazy Content

```csharp
var snapshot = await agent.StartAsync("https://test.example.com/feed");

while (snapshot.HasMoreContent)
{
    // Process current content...
    ProcessContent(snapshot.Content);

    // Scroll to load more
    snapshot = await agent.ExecuteAsync(
        new BrowserAction(EBrowserActionType.ScrollDown));
}
```

## Formatting the Prompt

A typical system prompt for the LLM:

```
You are a web browser agent. After each action you receive a PageSnapshot
with the page content and numbered interactive elements.

To interact, respond with a JSON BrowserAction:
- Navigate: {"Type": "Navigate", "Value": "https://..."}
- Click:    {"Type": "Click", "ElementIndex": 3}
- Fill:     {"Type": "Fill", "ElementIndex": 2, "Value": "hello"}
- FillForm: {"Type": "FillForm", "Fields": [{"ElementIndex": 2, "Value": "x"}]}
- Submit:   {"Type": "Submit", "ElementIndex": 4}
- Scroll:   {"Type": "ScrollDown"}
- Done:     null (when the task is complete)

If you see an Error in the snapshot, adjust your approach.
```

## Action History

The agent tracks all actions for context:

```csharp
// After several actions...
var history = agent.ActionHistory;
// Pass to LLM as conversation context if needed
```

## Error Recovery

The LLM can recover from errors because the session stays alive:

| Error | Recovery strategy |
| --- | --- |
| "Element index N not found" | Re-read elements, pick correct index |
| "HTTP 404" | Try a different URL |
| "Timeout" | Try ScrollDown or WaitFor |
| "Action limit reached" | Task is done — report results |

## Tips

- Use `EContentFormat.Markdown` (default) for best token/structure balance
- Set `IncludeScreenshot = true` only for multimodal LLMs that benefit from visual context
- Use `StorageStatePath` for flows that require authentication across sessions
- Keep `MaxActions` reasonable (50–100) to prevent runaway loops
- The agent is not thread-safe — use one instance per conversation thread
