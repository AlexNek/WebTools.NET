# WebNavigationService

`WebNavigationService` loads a page, extracts same-host links, and verifies
which links actually work. It is a caller-agnostic service; it does not own or
create a browser session.

## Construction

```csharp
using WebTools.NET;
using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;

await using var browser = new PlaywrightSession();
var navigation = new WebNavigationService(browser);
```

The caller owns the supplied `IBrowserInteraction` lifetime.

## NavigateAsync — discover working links

Navigates to a start URL, extracts absolute links that point to the **same
host**, then probes each candidate for reachability:

```csharp
var workingLinks = await navigation.NavigateAsync(
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

### Link extraction rules

- Only `href` values that resolve to the **same host** as the start URL are
  kept; cross-host links are ignored
- Relative and protocol-relative URLs are resolved against the page URL
- `javascript:`, `mailto:`, `tel:`, `#`, `whatsapp:`, and `ftp:` links are
  skipped
- Duplicates are removed case-insensitively
- Each candidate is verified through the browser session's reachability check
  before it is returned

### Failure behavior

`NavigateAsync` returns an empty list for operational problems such as a page
that cannot be loaded or whose HTML cannot be read. Cancellation is propagated.

## ClickAndExtractAsync — links after an interaction

Clicks a selector on the current page and returns absolute same-host links
from the resulting page **without** reachability verification:

```csharp
await navigation.NavigateAsync("https://test.example.com");
var links = await navigation.ClickAndExtractAsync("a.next-page", maxLinks: 30);
```

| Parameter | Default | Description |
| --- | --- | --- |
| `selector` | — | CSS selector of the element to click |
| `maxLinks` | `30` | Maximum number of links to return |
| `ct` | `default` | Cancellation token |

Use this to step through paginated content or reveal links hidden behind a
click. It applies the same extraction rules as `NavigateAsync`, but returns the
raw extracted links.

## Lifetime

`WebNavigationService` does not own the browser session. The caller disposes
the supplied session after all navigation operations are complete.
