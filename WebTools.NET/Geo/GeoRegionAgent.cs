using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;

namespace WebTools.NET.Geo;

/// <summary>
/// Legacy geo-region facade retained for source compatibility.
/// Use <see cref="GeoRegionService"/> for new code.
/// </summary>
[Obsolete("Use GeoRegionService instead.")]
public sealed class GeoRegionAgent : IDisposable, IGeoRegionProvider
{
    private readonly GeoRegionService _service;

    public GeoRegionAgent()
    {
        _service = new GeoRegionService();
    }

    public GeoRegionAgent(
        HttpClient http,
        ILogger<GeoRegionAgent>? logger = null)
    {
        _ = logger;
        _service = new GeoRegionService(http);
    }

    public Task<string> DetectRegionAsync(CancellationToken ct = default) =>
        _service.DetectRegionAsync(ct);

    public void Dispose() => _service.Dispose();
}
