using WebTools.NET.Models;

namespace WebTools.NET.Abstractions;

public interface IWebSearchProvider
{
    Task<SearchResult> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default);
}
