using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

namespace WebTools.NET;

public sealed partial class WebNavigationService
{
    private static readonly string[] SkippedPrefixes =
            ["javascript:", "mailto:", "tel:", "#", "whatsapp:", "ftp:"];

    private readonly IBrowserInteraction _browser;

    private readonly ILogger<WebNavigationService>? _logger;

    public WebNavigationService(
        IBrowserInteraction browser,
        ILogger<WebNavigationService>? logger = null)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _logger = logger;
    }

    /// <summary>
    /// Returns the interactive elements exposed by the current browser page.
    /// </summary>
    public Task<IReadOnlyList<InteractiveElement>> GetPageElementsAsync(CancellationToken ct = default)
    {
        if (_browser is not IBrowserElementExtractor elementExtractor)
        {
            throw new InvalidOperationException(
                "The browser must implement IBrowserElementExtractor to extract page elements.");
        }

        return elementExtractor.GetInteractiveElementsAsync(ct);
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
        ArgumentOutOfRangeException.ThrowIfNegative(maxLinks);
        _logger?.LogDebug("Clicking '{Selector}' and extracting links", selector);

        try
        {
            await _browser.ClickAsync(selector, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        catch (OperationCanceledException)
        {
            throw;
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
        ArgumentOutOfRangeException.ThrowIfNegative(maxLinks);
        _logger?.LogDebug("Navigating from {Url}", startUrl);

        try
        {
            await _browser.NavigateAsync(startUrl, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Failed to load {Url}: {Error}", startUrl, ex.Message);
            return [];
        }

        string html, currentUrl;
        try
        {
            html = await _browser.GetHtmlAsync(ct);
            currentUrl = await _browser.GetCurrentUrlAsync(ct);
            if (string.IsNullOrWhiteSpace(currentUrl))
            {
                currentUrl = startUrl;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Failed to get page HTML or current URL: {Error}", ex.Message);
            return [];
        }

        var links = ExtractAbsoluteLinks(html, currentUrl);
        _logger?.LogDebug("Extracted {Count} absolute links", links.Count);

        if (links.Count == 0)
        {
            return [];
        }

        var working = new List<string>();
        var tested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in links.Take(maxLinks))
        {
            ct.ThrowIfCancellationRequested();

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
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            _logger?.LogDebug("Failed to parse base URL: {BaseUrl}", baseUrl);
            return [];
        }

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
