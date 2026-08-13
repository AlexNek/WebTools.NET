using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;

namespace WebTools.NET;

public sealed partial class WebNavigationAgent : IAsyncDisposable
{
    private static readonly string[] SkippedPrefixes =
            ["javascript:", "mailto:", "tel:", "#", "whatsapp:", "ftp:"];

    private readonly IBrowserInteraction _browser;

    private readonly ILogger<WebNavigationAgent>? _logger;

    private readonly PlaywrightSession? _ownedBrowser;

    public WebNavigationAgent()
    {
        _ownedBrowser = new PlaywrightSession();
        _browser = _ownedBrowser;
    }

    public WebNavigationAgent(
        IBrowserInteraction browser,
        ILogger<WebNavigationAgent>? logger = null)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownedBrowser is not null)
        {
            await _ownedBrowser.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clicks the given selector and returns absolute same-host links found on the
    /// resulting page (links to other hosts are ignored).
    /// </summary>
    public async Task<IReadOnlyList<string>> ClickAndExtractAsync(
        string selector,
        int maxLinks = 30,
        CancellationToken ct = default)
    {
        _logger?.LogDebug("Clicking '{Selector}' and extracting links", selector);

        try
        {
            await _browser.ClickAsync(selector, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Click failed: {Error}", ex.Message);
            return [];
        }

        string html, currentUrl;
        try
        {
            html = await _browser.GetHtmlAsync(ct);
            currentUrl = await _browser.GetCurrentUrlAsync(ct);
        }
        catch
        {
            return [];
        }

        return ExtractAbsoluteLinks(html, currentUrl)
            .Take(maxLinks)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Navigates to <paramref name="startUrl"/> and returns the reachable absolute
    /// same-host links found on that page (links to other hosts are ignored).
    /// </summary>
    public async Task<IReadOnlyList<string>> NavigateAsync(
        string startUrl,
        int maxLinks = 30,
        CancellationToken ct = default)
    {
        _logger?.LogDebug("Navigating from {Url}", startUrl);

        try
        {
            await _browser.NavigateAsync(startUrl, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Failed to load {Url}: {Error}", startUrl, ex.Message);
            return [];
        }

        string html;
        try
        {
            html = await _browser.GetHtmlAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Failed to get page HTML: {Error}", ex.Message);
            return [];
        }

        var links = ExtractAbsoluteLinks(html, startUrl);
        _logger?.LogDebug("Extracted {Count} absolute links", links.Count);

        if (links.Count == 0)
        {
            return [];
        }

        var working = new List<string>();
        var tested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in links.Take(maxLinks))
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!tested.Add(url))
            {
                continue;
            }

            if (await _browser.CheckReachabilityAsync(url, ct))
            {
                working.Add(url);
                _logger?.LogDebug("  OK: {Url}", url);
            }
        }

        _logger?.LogDebug("Navigation complete: {Count} working URLs", working.Count);
        return working.AsReadOnly();
    }

    private List<string> ExtractAbsoluteLinks(string html, string baseUrl)
    {
        var baseUri = new Uri(baseUrl);
        var links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var matches = HrefRegex().Matches(html);
        foreach (Match m in matches)
        {
            var href = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var skip = false;
            foreach (var prefix in SkippedPrefixes)
            {
                if (href.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    skip = true;
                    break;
                }
            }

            if (skip)
            {
                continue;
            }

            try
            {
                Uri absolute;
                if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    absolute = new Uri(href);
                }
                else if (href.StartsWith("//"))
                {
                    absolute = new Uri(baseUri.Scheme + ":" + href);
                }
                else
                {
                    absolute = new Uri(baseUri, href);
                }

                if (absolute.Host == baseUri.Host)
                {
                    links.Add(absolute.ToString());
                }
            }
            catch
            {
                _logger?.LogDebug("Failed to parse URL: {Href}", href);
            }
        }

        return links.ToList();
    }

    [GeneratedRegex(
        @"<a[^>]+href\s*=\s*[""']([^""']*)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HrefRegex();
}
