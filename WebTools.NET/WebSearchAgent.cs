using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;
using WebTools.NET.Search;

namespace WebTools.NET;

public sealed class WebSearchAgent : IAsyncDisposable
{
    private readonly ILogger<WebSearchAgent>? _logger;

    private readonly PlaywrightSearchProvider? _ownedSearch;

    private readonly IWebSearchProvider _search;

    public WebSearchAgent()
    {
        _ownedSearch = new PlaywrightSearchProvider();
        _search = _ownedSearch;
    }

    public WebSearchAgent(IWebSearchProvider search, ILogger<WebSearchAgent>? logger = null)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownedSearch is not null)
        {
            await _ownedSearch.DisposeAsync().ConfigureAwait(false);
        }
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

        foreach (var fq in GenerateFallbackQueries(query))
        {
            result = await _search.SearchAsync(fq, maxResults, ct);
            if (result.Success && result.Results.Count > 0)
            {
                return result;
            }
        }

        return result;
    }

    private static string[] GenerateFallbackQueries(string query)
    {
        return query.Contains(" API", StringComparison.OrdinalIgnoreCase)
                   ? [query.Replace(" API", ""), query + " official site"]
                   : [query + " official site"];
    }
}
