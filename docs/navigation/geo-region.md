# Geo-awareness

`GeoRegionAgent` detects the broad region the application is running in, so
agents can pick region-appropriate endpoints and sources.

## Regions

| Region code | Meaning |
| --- | --- |
| `us` | United States |
| `eu` | European Union member states |
| `china` | China |
| `intl` | Everywhere else |

## Detection Strategy

1. **Geo-IP lookup** — queries a free Geo-IP service and maps the country
   code to a region (EU country list, `CN`, `US`; everything else `intl`)
2. **Locale fallback** — when the lookup fails, derives the region from the
   system UI culture: `zh-*` cultures map to `china`, `en-US` / `*-US` to
   `us`, and all others to `intl`

The result is cached after the first successful detection; later calls return
the cached value without network traffic.

!!! warning
    The free Geo-IP tier only supports plain HTTP, so the lookup request is
    not encrypted and its result must not be treated as authoritative. It is
    a best-effort hint, not a security signal.

## Usage

```csharp
using WebTools.NET.Geo;

using var geo = new GeoRegionAgent();
var region = await geo.DetectRegionAsync();

var source = region switch
    {
        "china" => "https://mirror.example.com",
        _       => "https://test.example.com"
    };
```

The constructor accepts an optional `HttpClient` (for testing) and an
optional `ILogger<GeoRegionAgent>`.

## The Abstraction

`GeoRegionAgent` implements `IGeoRegionProvider`, so components that only
need the region can depend on the interface instead:

```csharp
public interface IGeoRegionProvider
{
    Task<string> DetectRegionAsync(CancellationToken ct = default);
}
```
