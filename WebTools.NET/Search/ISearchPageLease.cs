using Microsoft.Playwright;

namespace WebTools.NET.Search;

internal interface ISearchPageLease : IAsyncDisposable
{
    IPage Page { get; }
}
