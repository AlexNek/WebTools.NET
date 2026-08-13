using System.Text.RegularExpressions;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Search;

public sealed partial class DuckDuckGoSearchProvider : IWebSearchProvider, IDisposable
{
    private readonly HttpClient _http;

    public DuckDuckGoSearchProvider()
    {
        _http = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(10),
                        DefaultRequestHeaders =
                            {
                                {
                                    "User-Agent",
                                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
                                },
                                { "Accept", "text/html,application/xhtml+xml" }
                            }
                    };
    }

    public DuckDuckGoSearchProvider(HttpClient http)
    {
        _http = http;
    }

    public void Dispose() => _http.Dispose();

    public async Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            using var resp = await _http.GetAsync(url, ct);

            if (!resp.IsSuccessStatusCode)
                return new SearchResult(false, [], $"HTTP {(int)resp.StatusCode}");

            var html = await resp.Content.ReadAsStringAsync(ct);
            var results = ParseResults(html, maxResults);
            return new SearchResult(true, results, null);
        }
        catch (Exception ex)
        {
            return new SearchResult(false, [], ex.Message);
        }
    }

    [GeneratedRegex(
        @"<a[^>]+rel=""nofollow""[^>]*class=""result__a""[^>]*href=""([^""]+)""[^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkRegex();

    private static IReadOnlyList<SearchResultItem> ParseResults(string html, int maxResults)
    {
        var items = new List<SearchResultItem>();
        var anchors = LinkRegex().Matches(html);

        foreach (Match match in anchors)
        {
            if (items.Count >= maxResults) break;

            var url = match.Groups[1].Value;
            var snippet = match.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(url) || url.StartsWith('/'))
                continue;

            var title = TitleRegex().Match(match.Groups[0].Value).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            items.Add(
                new SearchResultItem(
                    System.Net.WebUtility.HtmlDecode(title),
                    System.Net.WebUtility.HtmlDecode(url),
                    System.Net.WebUtility.HtmlDecode(StripTags(snippet))));
        }

        return items.AsReadOnly();
    }

    private static string StripTags(string html) => TagRegex().Replace(html, " ").Trim();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(
        @"<a[^>]+class=""result__a""[^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
}
