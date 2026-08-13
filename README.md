# WebTools.NET

Web tools for .NET agents: web search, browser-based content fetching, and navigation.

## Features

- **Web Search** – search the web using DuckDuckGo or browser-based providers (Playwright, CloakBrowser).
- **Content Fetching** – retrieve and parse web page content via headless browsers.
- **Navigation Agent** – autonomous web navigation with geo-region awareness.
- **Dependency Injection** – easy integration via `IServiceCollection` extensions.

## Installation

```bash
dotnet add package WebTools.NET
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using WebTools.NET;

var services = new ServiceCollection();
services.AddWebTools();

var provider = services.BuildServiceProvider();
var webAccess = provider.GetRequiredService<IWebAccessService>();
```

## License

MIT – see [LICENSE.txt](LICENSE.txt) for details.
