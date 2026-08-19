# Geo-awareness

`GeoRegionService` detects the broad region where the application is running,
so callers can choose region-appropriate endpoints and sources.

## Migrating from `GeoRegionAgent`

`GeoRegionAgent` remains available as an obsolete forwarding wrapper. Prefer
`GeoRegionService` for new code. When an `HttpClient` is injected, both the
legacy wrapper and the preferred service leave it caller-owned; only an
internally created client is disposed by the owning service. See the
[Migration from Agent APIs](../getting-started/migration.md) guide for the
complete mapping.

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

using var geo = new GeoRegionService();
var region = await geo.DetectRegionAsync();

var source = region switch
    {
        "china" => "https://mirror.example.com",
        _       => "https://test.example.com"
    };
```

The constructor accepts an optional `HttpClient` (for testing) and an
optional `ILogger<GeoRegionService>`.

## The Abstraction

`GeoRegionService` implements `IGeoRegionProvider`, so components that only
need the region can depend on the interface instead:

```csharp
public interface IGeoRegionProvider
{
    Task<string> DetectRegionAsync(CancellationToken ct = default);
}
```
