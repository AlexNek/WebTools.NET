using Microsoft.Extensions.DependencyInjection.Extensions;

using WebTools.NET;
using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Models;
using WebTools.NET.Search;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering core WebTools.NET services.
/// </summary>
public static class WebToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers browser-backed content/search services, legacy low-level browser
    /// services, and a factory for creating isolated browser-session sessions.
    /// </summary>
    public static IServiceCollection AddBrowserServices(
        this IServiceCollection services,
        EBrowserEngine engine = EBrowserEngine.Playwright,
        bool headless = true,
        BrowserSessionOptions? browserSessionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IBrowserSessionFactory>(_ =>
            new BrowserSessionFactory(
                engine,
                headless,
                browserSessionOptions?.StorageStatePath,
                browserSessionOptions));
        services.TryAddSingleton<IBrowserAgentSessionFactory>(sp =>
            (IBrowserAgentSessionFactory)sp.GetRequiredService<IBrowserSessionFactory>());

        switch (engine)
        {
            case EBrowserEngine.CloakBrowser:
                services.TryAddSingleton<IWebContentFetcher>(_ =>
                    new CloakBrowserContentFetcher(headless));
                services.TryAddSingleton<IWebSearchProvider>(sp => new CloakBrowserSearchProvider(
                    sp.GetService<Logging.ILogger<CloakBrowserSearchProvider>>(),
                    headless));
                services.TryAddSingleton<CloakBrowserSession>(_ =>
                    new CloakBrowserSession(
                        storageStatePath: browserSessionOptions?.StorageStatePath,
                        headless: headless,
                        options: browserSessionOptions));
                services.TryAddSingleton<IBrowserInteraction>(sp =>
                    sp.GetRequiredService<CloakBrowserSession>());
                services.TryAddSingleton<IBrowserAgentInteraction>(sp =>
                    sp.GetRequiredService<CloakBrowserSession>());
                services.TryAddSingleton<IBrowserContent>(sp =>
                    sp.GetRequiredService<CloakBrowserSession>());
                break;

            case EBrowserEngine.Playwright:
                services.TryAddSingleton<IWebContentFetcher>(_ =>
                    new PlaywrightContentFetcher(headless));
                services.TryAddSingleton<IWebSearchProvider>(sp => new PlaywrightSearchProvider(
                    sp.GetService<Logging.ILogger<PlaywrightSearchProvider>>(),
                    headless));
                services.TryAddSingleton<PlaywrightSession>(_ =>
                    new PlaywrightSession(
                        storageStatePath: browserSessionOptions?.StorageStatePath,
                        headless: headless,
                        options: browserSessionOptions));
                services.TryAddSingleton<IBrowserInteraction>(sp =>
                    sp.GetRequiredService<PlaywrightSession>());
                services.TryAddSingleton<IBrowserAgentInteraction>(sp =>
                    sp.GetRequiredService<PlaywrightSession>());
                services.TryAddSingleton<IBrowserContent>(sp =>
                    sp.GetRequiredService<PlaywrightSession>());
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unknown browser engine.");
        }

        return services;
    }

    /// <summary>
    /// Legacy overload accepting the former browser-agent options.
    /// </summary>
    [Obsolete("Use BrowserSessionOptions with the browserSessionOptions parameter instead.")]
    public static IServiceCollection AddBrowserServices(
        this IServiceCollection services,
        BrowserAgentOptions browserAgentOptions) =>
        AddBrowserServices(
            services,
            EBrowserEngine.Playwright,
            headless: true,
            ToSessionOptions(browserAgentOptions));

    /// <summary>Legacy overload retaining the former engine/options call shape.</summary>
    [Obsolete("Use BrowserSessionOptions with the browserSessionOptions parameter instead.")]
    public static IServiceCollection AddBrowserServices(
        this IServiceCollection services,
        EBrowserEngine engine,
        BrowserAgentOptions? browserAgentOptions) =>
        AddBrowserServices(services, engine, headless: true, ToSessionOptions(browserAgentOptions));

    /// <summary>Legacy overload retaining the former headless/options call shape.</summary>
    [Obsolete("Use BrowserSessionOptions with the browserSessionOptions parameter instead.")]
    public static IServiceCollection AddBrowserServices(
        this IServiceCollection services,
        bool headless,
        BrowserAgentOptions? browserAgentOptions) =>
        AddBrowserServices(services, EBrowserEngine.Playwright, headless, ToSessionOptions(browserAgentOptions));

    /// <summary>Legacy overload retaining the former full call shape.</summary>
    [Obsolete("Use BrowserSessionOptions with the browserSessionOptions parameter instead.")]
    public static IServiceCollection AddBrowserServices(
        this IServiceCollection services,
        EBrowserEngine engine,
        bool headless,
        BrowserAgentOptions? browserAgentOptions) =>
        AddBrowserServices(services, engine, headless, ToSessionOptions(browserAgentOptions));

    private static BrowserSessionOptions? ToSessionOptions(BrowserAgentOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var nested = options.SessionOptions;
        return new BrowserSessionOptions
        {
            MaxOperations = nested?.MaxOperations ?? 50,
            MaxDuration = nested?.MaxDuration ?? TimeSpan.FromMinutes(5),
            DefaultFormat = nested?.DefaultFormat ?? EContentFormat.Markdown,
            IncludeScreenshot = nested?.IncludeScreenshot ?? false,
            StorageStatePath = options.StorageStatePath ?? nested?.StorageStatePath,
            ViewportWidth = nested?.ViewportWidth ?? 1920,
            ViewportHeight = nested?.ViewportHeight ?? 1080
        };
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
