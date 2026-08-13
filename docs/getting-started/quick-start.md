# Quick Start

This page shows the shortest path to each core capability. All examples use
fake placeholder URLs.

## 1. Register Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using WebTools.NET.Abstractions;

var services = new ServiceCollection();

services.AddWebToolsCore();                       // IWebAccessService
services.AddBrowserServices();                    // Playwright engine (default)

var provider = services.BuildServiceProvider();
```

See [Dependency Injection](dependency-injection.md) for engine selection and
headless options.

## 2. Check URL Reachability

```csharp
var webAccess = provider.GetRequiredService<IWebAccessService>();

var check = await webAccess.CheckReachabilityAsync("https://test.example.com");
Console.WriteLine(check.Reachable ? "Reachable" : check.ErrorMessage);
```

## 3. Search the Web

```csharp
using WebTools.NET.Search;

using var ddg = new DuckDuckGoSearchProvider();
await using var search = new WebSearchAgent(ddg);

var result = await search.SearchAsync("dotnet web scraping", maxResults: 5);
if (result.Success)
{
    foreach (var item in result.Results)
    {
        Console.WriteLine($"{item.Title} -> {item.Url}");
    }
}
```

## 4. Fetch Page Content

```csharp
var fetcher = provider.GetRequiredService<IWebContentFetcher>();

var content = await fetcher.FetchAsync("https://test.example.com");
if (content.Success)
{
    Console.WriteLine(content.Content);   // plain-text page content
    Console.WriteLine(content.FinalUrl);  // URL after redirects
}
```

## 5. Navigate and Extract Links

```csharp
using WebTools.NET;

await using var agent = new WebNavigationAgent();

var links = await agent.NavigateAsync("https://test.example.com", maxLinks: 20);
foreach (var link in links)
{
    Console.WriteLine(link);   // verified reachable same-host links
}
```

## Next Steps

- [Web Search](../search/overview.md) — providers, fallback queries
- [Content Fetching](../content-fetching/overview.md) — engines and timeouts
- [WebNavigationAgent](../navigation/web-navigation-agent.md) — navigation
  behavior in detail
