using Microsoft.Playwright;

namespace WebTools.NET.Search;

internal sealed class SearchPageLease : ISearchPageLease
{
    private readonly IPage _page;
    private readonly Func<ValueTask> _disposeResourcesAsync;
    private readonly Func<ValueTask> _releaseAsync;
    private int _disposed;

    internal SearchPageLease(
        IPage page,
        Func<ValueTask> disposeResourcesAsync,
        Func<ValueTask> releaseAsync)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _disposeResourcesAsync = disposeResourcesAsync ?? throw new ArgumentNullException(nameof(disposeResourcesAsync));
        _releaseAsync = releaseAsync ?? throw new ArgumentNullException(nameof(releaseAsync));
    }

    public IPage Page => _page;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await DisposeQuietlyAsync(_page).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await _disposeResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Cleanup must not replace the search result or mask the original failure.
            }
            finally
            {
                try
                {
                    await _releaseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Ownership release is best effort after cleanup has completed.
                }
            }
        }
    }

    private static async ValueTask DisposeQuietlyAsync(IAsyncDisposable disposable)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cleanup must not replace the search result or mask the original failure.
        }
    }
}
