using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;
using WebTools.NET.Search;

namespace WebTools.NET;

public sealed class WebSearchAgent
{
    private static readonly PlaywrightSearchProvider DefaultSearch = new();

    private readonly ILogger<WebSearchAgent>? _logger;

    private readonly IWebSearchProvider _search;

    public WebSearchAgent()
        : this(DefaultSearch)
    {
    }

    public WebSearchAgent(IWebSearchProvider search, ILogger<WebSearchAgent>? logger = null)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _logger = logger;
    }

    public async Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var result = await _search.SearchAsync(query, maxResults, ct);

        if (result.Success && result.Results.Count > 0)
        {
            return result;
        }

        _logger?.LogDebug(
            "Initial search '{Query}' returned no results, trying fallback queries",
            query);

        foreach (var fq in TryGenerateFallbackQueries(query))
        {
            result = await _search.SearchAsync(fq, maxResults, ct);
            if (result.Success && result.Results.Count > 0)
            {
                return result;
            }
        }

        return result;
    }

    private static string[] TryGenerateFallbackQueries(string query)
    {
        return query.Contains(" API", StringComparison.OrdinalIgnoreCase)
                   ? [query.Replace(" API", ""), query + " official site", query]
                   : [query + " official site", query];
    }
}
