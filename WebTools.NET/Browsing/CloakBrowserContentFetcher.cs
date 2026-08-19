using CloakBrowser;

using Microsoft.Playwright;

using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

public sealed class CloakBrowserContentFetcher : BrowserContentFetcherBase
{
    private const string CloakNotInstalledMessage =
        "CloakBrowser binary not found. First launch should auto-download, or run: dotnet cloakbrowser install";

    private readonly bool _headless;
    private readonly string? _warmupUrl;

    private CloakBrowserHandle? _handle;

    protected override string BrowserNotInstalledMessage => CloakNotInstalledMessage;
    protected override string? WarmupUrl => _warmupUrl;

    public CloakBrowserContentFetcher(bool headless = true, string? warmupUrl = null)
    {
        _headless = headless;
        _warmupUrl = warmupUrl;
    }

    protected override async Task<IBrowserContext> CreateContextAsync(CancellationToken ct)
    {
        CloakBrowserHandle? handle = null;
        try
        {
            handle = await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = _headless })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            var context = await handle.RawBrowser
                .NewContextAsync(BrowserFetcherDefaults.CreateContextOptions())
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            _handle = handle;
            return context;
        }
        catch
        {
            if (handle is not null)
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    protected override async Task DisposeBrowserResourcesAsync()
    {
        var handle = Interlocked.Exchange(ref _handle, null);
        if (handle is not null)
        {
            try
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException)
            {
            }
        }
    }
}
