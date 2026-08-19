using CloakBrowser;

using Microsoft.Playwright;

using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Browser session implementation using CloakBrowser for anti-detection.
/// </summary>
public sealed class CloakBrowserSession : BrowserSessionBase
{
    private readonly bool _headless;

    private CloakBrowserHandle? _handle;

    public CloakBrowserSession(
        string? storageStatePath = null,
        bool headless = true,
        BrowserSessionOptions? options = null)
        : base(storageStatePath, options)
    {
        _headless = headless;
    }

    protected override async Task<(IBrowserContext Context, IPage Page)> CreatePageAsync(CancellationToken ct)
    {
        _handle ??= await CloakLauncher.LaunchAsync(new LaunchOptions { Headless = _headless })
            .AwaitWithCancellationAsync(ct)
            .ConfigureAwait(false);

        var browser = _handle.RawBrowser;
        IBrowserContext? context = null;
        try
        {
            context = await browser.NewContextAsync(CreateContextOptions(StorageStatePath))
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            var page = await context.NewPageAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            var created = (Context: context, Page: page);
            context = null;
            return created;
        }
        catch
        {
            await CloseContextAsync(context).ConfigureAwait(false);
            throw;
        }
    }

    protected override async Task DisposeResourcesAsync()
    {
        if (_handle is not null)
        {
            try
            {
                await _handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
            {
            }
        }

        _handle = null;
    }
}
