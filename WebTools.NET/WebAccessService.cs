using System.Net;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET;

public sealed class WebAccessService : IWebAccessService, IDisposable
{
    private const int DefaultMaxRedirects = 10;

    private readonly CookieContainer _cookies = new();

    private readonly HttpClient _httpClient;

    public WebAccessService()
    {
        var handler = new HttpClientHandler
                          {
                              AllowAutoRedirect = false,
                              AutomaticDecompression =
                                  DecompressionMethods.GZip | DecompressionMethods.Deflate,
                              UseCookies = true,
                              CookieContainer = _cookies
                          };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add(
            "Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    internal WebAccessService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UrlCheckResult> CheckReachabilityAsync(
        string url,
        CancellationToken ct = default)
    {
        try
        {
            return await CheckWithRedirectTrackingAsync(url, DefaultMaxRedirects, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new UrlCheckResult(false, null, "Timed out");
        }
        catch (HttpRequestException ex)
        {
            return new UrlCheckResult(false, null, $"HTTP request failed: {ex.Message}");
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<UrlCheckResult> CheckWithRedirectTrackingAsync(
        string url,
        int maxRedirects,
        CancellationToken ct)
    {
        var currentUrl = url;
        var redirectCount = 0;

        for (var i = 0; i <= maxRedirects; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            using var response = await _httpClient.SendAsync(
                                     request,
                                     HttpCompletionOption.ResponseHeadersRead,
                                     ct);

            var status = (int)response.StatusCode;

            if (HttpStatusHelper.IsRedirect(status))
            {
                var location = response.Headers.Location;
                if (location is null)
                {
                    return new UrlCheckResult(
                        false,
                        status,
                        $"HTTP {status} redirect missing Location header",
                        redirectCount,
                        currentUrl);
                }

                currentUrl = location.IsAbsoluteUri
                                 ? location.ToString()
                                 : new Uri(new Uri(currentUrl), location).ToString();
                redirectCount++;
                continue;
            }

            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? currentUrl;
            var reachable = HttpStatusHelper.IsSuccessOrRedirect(status);

            return new UrlCheckResult(
                reachable,
                status,
                reachable ? null : $"HTTP {status}",
                redirectCount,
                finalUrl);
        }

        return new UrlCheckResult(
            false,
            null,
            $"Too many redirects ({redirectCount})",
            redirectCount,
            currentUrl);
    }
}
