using Microsoft.Extensions.DependencyInjection.Extensions;

using WebTools.NET;
using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Search;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering core WebTools.NET services.
/// </summary>
public static class WebToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers IWebContentFetcher, IWebSearchProvider, and IBrowserInteraction
    /// using the specified browser engine.
    /// </summary>
    public static IServiceCollection AddBrowserServices(
        this IServiceCollection services,
        EBrowserEngine engine = EBrowserEngine.Playwright,
        bool headless = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        switch (engine)
        {
            case EBrowserEngine.CloakBrowser:
                services.TryAddSingleton<IWebContentFetcher>(_ =>
                    new CloakBrowserContentFetcher(headless));
                services.TryAddSingleton<IWebSearchProvider>(sp => new CloakBrowserSearchProvider(
                    sp.GetService<Logging.ILogger<CloakBrowserSearchProvider>>(),
                    headless));
                services.TryAddSingleton<IBrowserInteraction, CloakBrowserSession>();
                break;

            default:
                services.TryAddSingleton<IWebContentFetcher>(_ =>
                    new PlaywrightContentFetcher(headless));
                services.TryAddSingleton<IWebSearchProvider>(sp => new PlaywrightSearchProvider(
                    sp.GetService<Logging.ILogger<PlaywrightSearchProvider>>(),
                    headless));
                services.TryAddSingleton<IBrowserInteraction, PlaywrightSession>();
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers core web tool abstractions and their default implementations.
    /// </summary>
    public static IServiceCollection AddWebToolsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWebAccessService, WebAccessService>();

        return services;
    }
}
