using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

namespace WebTools.NET;

/// <summary>
/// Legacy web-navigation facade retained for source compatibility.
/// Use <see cref="WebNavigationService"/> for new code.
/// </summary>
[Obsolete("Use WebNavigationService instead.")]
public sealed class WebNavigationAgent : IAsyncDisposable
{
    private readonly PlaywrightSession? _ownedBrowser;

    private readonly WebNavigationService _service;

    public WebNavigationAgent()
    {
        _ownedBrowser = new PlaywrightSession();
        _service = new WebNavigationService(_ownedBrowser);
    }

    public WebNavigationAgent(
        IBrowserInteraction browser,
        ILogger<WebNavigationAgent>? logger = null)
    {
        _ = logger;
        _service = new WebNavigationService(browser);
    }

    public ValueTask DisposeAsync() => _ownedBrowser is null
        ? ValueTask.CompletedTask
        : _ownedBrowser.DisposeAsync();

    public Task<IReadOnlyList<InteractiveElement>> GetPageElementsAsync(CancellationToken ct = default) =>
        _service.GetPageElementsAsync(ct);

    public Task<IReadOnlyList<string>> ClickAndExtractAsync(
        string selector,
        int maxLinks = 30,
        CancellationToken ct = default) =>
        _service.ClickAndExtractAsync(selector, maxLinks, ct);

    public Task<IReadOnlyList<string>> NavigateAsync(
        string startUrl,
        int maxLinks = 30,
        CancellationToken ct = default) =>
        _service.NavigateAsync(startUrl, maxLinks, ct);
}
