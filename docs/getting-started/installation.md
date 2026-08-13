# Installation

## Requirements

- .NET 10.0 or later
- The [WebTools.NET](https://www.nuget.org/packages/WebTools.NET) NuGet package

## Add the Package

=== "dotnet CLI"

    ```bash
    dotnet add package WebTools.NET
    ```

=== "PackageReference"

    ```xml
    <PackageReference Include="WebTools.NET" Version="*" />
    ```

=== "Package Manager Console"

    ```powershell
    Install-Package WebTools.NET
    ```

## Install Playwright Browsers

Browser-based features (content fetching, browser search providers, the
navigation agent) drive Chromium through Microsoft Playwright. After adding the
package, install the Playwright browser binaries once per machine:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

Adjust the path to your build output folder (e.g. `bin/Release/net10.0/`). The
script is provided by the `Microsoft.Playwright` dependency that WebTools.NET
pulls in transitively.

!!! note
    `DuckDuckGoSearchProvider` and `IWebAccessService` use plain HTTP and do
    not require Playwright browsers.

## CloakBrowser Engine

If you plan to use the CloakBrowser engine (see
[Browser Engines](../concepts/browser-engines.md)), sessions are launched
through `CloakLauncher` from the `CloakBrowser` package. Refer to the
CloakBrowser documentation for its browser setup requirements.

## Next Step

Continue with the [Quick Start](quick-start.md).
