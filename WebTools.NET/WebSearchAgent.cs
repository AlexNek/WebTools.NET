using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;
using WebTools.NET.Search;

namespace WebTools.NET;

/// <summary>
/// Legacy web-search facade retained for source compatibility.
/// Use <see cref="WebSearchService"/> for new code.
/// </summary>
[Obsolete("Use WebSearchService instead.")]
public sealed class WebSearchAgent : IAsyncDisposable
{
    private readonly PlaywrightSearchProvider? _ownedSearch;

    private readonly WebSearchService _service;

    public WebSearchAgent()
    {
        _ownedSearch = new PlaywrightSearchProvider();
        _service = new WebSearchService(_ownedSearch);
    }

    public WebSearchAgent(
        IWebSearchProvider search,
        ILogger<WebSearchAgent>? logger = null)
    {
        _ = logger;
        _service = new WebSearchService(search);
    }

    public ValueTask DisposeAsync() => _ownedSearch is null
        ? ValueTask.CompletedTask
        : _ownedSearch.DisposeAsync();

    public Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken ct = default) =>
        _service.SearchAsync(query, maxResults, ct);
}
