using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;

namespace WebTools.NET.Geo;

/// <summary>
/// Detects the current region ("us", "eu", "china" or "intl") via a Geo-IP lookup,
/// falling back to the system UI culture. The result is cached after first detection.
/// </summary>
/// <remarks>
/// The free ip-api.com tier only supports plain HTTP, so the Geo-IP request is not
/// encrypted and its result should not be treated as authoritative.
/// </remarks>
public sealed class GeoRegionService : IDisposable, IGeoRegionProvider
{
    private const string GeoApiUrl = "http://ip-api.com/json/?fields=countryCode";

    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private readonly HttpClient _http;

    private readonly ILogger<GeoRegionService>? _logger;

    private readonly bool _ownsHttp;

    private string? _cached;

    public GeoRegionService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _ownsHttp = true;
    }

    public GeoRegionService(HttpClient http, ILogger<GeoRegionService>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger;
    }

    public async Task<string> DetectRegionAsync(CancellationToken ct = default)
    {
        var cached = _cached;
        if (cached is not null)
        {
            return cached;
        }

        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var region = await TryGeoIpAsync(ct)
                         ?? FallbackToLocale();

            _cached = region;
            _logger?.LogDebug("Detected region: {Region}", region);
            return region;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }

        _cacheLock.Dispose();
    }

    private static string FallbackToLocale()
    {
        var culture = CultureInfo.CurrentUICulture;
        var region = culture.Name switch
            {
                "zh-CN" or "zh-CHS" or "zh-CHT" => "china",
                "en-US" => "us",
                _ when culture.Name.EndsWith("-CN", StringComparison.OrdinalIgnoreCase) => "china",
                _ when culture.Name.EndsWith("-US", StringComparison.OrdinalIgnoreCase) => "us",
                _ => culture.TwoLetterISOLanguageName switch
                    {
                        "zh" => "china",
                        _ => "intl"
                    }
            };

        return region;
    }

    private static string MapCountryCode(string code)
    {
        return code.ToUpperInvariant() switch
            {
                "CN" => "china",
                "US" => "us",
                "DE" or "FR" or "GB" or "IT" or "ES" or "NL" or "BE" or "SE" or "NO"
                    or "DK" or "FI" or "PT" or "AT" or "CH" or "PL" or "IE" or "CZ"
                    or "HU" or "RO" or "GR" or "SK" or "BG" or "HR" or "LT" or "LV"
                    or "EE" or "SI" or "LU" or "CY" or "MT" => "eu",
                _ => "intl"
            };
    }

    private async Task<string?> TryGeoIpAsync(CancellationToken ct)
    {
        try
        {
            var json = await _http.GetStringAsync(GeoApiUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("countryCode", out var cc))
            {
                var code = cc.GetString() ?? "";
                return MapCountryCode(code);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Geo-IP lookup failed");
        }

        return null;
    }
}
