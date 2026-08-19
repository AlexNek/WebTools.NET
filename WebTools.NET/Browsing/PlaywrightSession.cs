using Microsoft.Playwright;

using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Browser session implementation using Playwright directly.
/// </summary>
public sealed class PlaywrightSession : BrowserSessionBase
{
    private IBrowser? _browser;

    private readonly bool _headless;

    private IPlaywright? _playwright;

    public PlaywrightSession(
        string? storageStatePath = null,
        bool headless = true,
        BrowserSessionOptions? options = null)
        : base(storageStatePath, options)
    {
        _headless = headless;
    }

    protected override async Task<(IBrowserContext Context, IPage Page)> CreatePageAsync(CancellationToken ct)
    {
        _playwright ??= await Playwright.CreateAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
        _browser ??= await _playwright.Chromium
            .LaunchAsync(new BrowserTypeLaunchOptions { Headless = _headless })
            .AwaitWithCancellationAsync(ct)
            .ConfigureAwait(false);

        IBrowserContext? context = null;
        try
        {
            context = await _browser.NewContextAsync(CreateContextOptions(StorageStatePath))
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
        if (_browser is not null)
        {
            try
            {
                await _browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
            {
            }
        }

        _playwright?.Dispose();
        _browser = null;
        _playwright = null;
    }
}
