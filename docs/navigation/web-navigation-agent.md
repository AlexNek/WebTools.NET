# WebNavigationAgent

`WebNavigationAgent` performs autonomous navigation: it loads a page, extracts
same-host links, and verifies which of them actually work.

## Construction

```csharp
using WebTools.NET;

// Owns a PlaywrightSession internally
await using var agent1 = new WebNavigationAgent();

// Wraps any IBrowserInteraction; caller owns its lifetime
await using var agent2 = new WebNavigationAgent(browserSession);
```

Both constructors also accept an optional `ILogger<WebNavigationAgent>`.

## NavigateAsync — Discover Working Links

Navigates to a start URL, extracts absolute links that point to the **same
host**, then probes each candidate for reachability:

```csharp
var workingLinks = await agent.NavigateAsync(
    "https://test.example.com",
    maxLinks: 30,
    ct: cancellationToken);

foreach (var link in workingLinks)
{
    Console.WriteLine(link);
}
```

| Parameter | Default | Description |
| --- | --- | --- |
| `startUrl` | — | Page to load and extract links from |
| `maxLinks` | `30` | Maximum number of candidate links to probe |
| `ct` | `default` | Cancellation token |

### Link Extraction Rules

- Only `href` values that resolve to the **same host** as the start URL are
  kept; cross-host links are ignored
- Relative and protocol-relative URLs are resolved against the page URL
- `javascript:`, `mailto:`, `tel:`, `#`, `whatsapp:`, and `ftp:` links are
  skipped
- Duplicates are removed (case-insensitive)
- Each candidate is verified through the browser session's reachability check
  before it is returned

### Failure Behavior

`NavigateAsync` never throws for operational problems — it returns an empty
list when the page cannot be loaded or its HTML cannot be read. See
[Error Handling](../concepts/error-handling.md).

## ClickAndExtractAsync — Links After an Interaction

Clicks a selector on the current page and returns absolute same-host links
from the resulting page **without** reachability verification:

```csharp
await agent.NavigateAsync("https://test.example.com");
var links = await agent.ClickAndExtractAsync("a.next-page", maxLinks: 30);
```

| Parameter | Default | Description |
| --- | --- | --- |
| `selector` | — | CSS selector of the element to click |
| `maxLinks` | `30` | Maximum number of links to return |
| `ct` | `default` | Cancellation token |

Use this to step through paginated content or reveal links hidden behind a
click. It applies the same extraction rules as `NavigateAsync`, but returns
the raw extracted links.

## Lifetime

Dispose the agent with `await using`. Disposal closes the internally owned
browser session; sessions passed into the constructor remain your
responsibility.
