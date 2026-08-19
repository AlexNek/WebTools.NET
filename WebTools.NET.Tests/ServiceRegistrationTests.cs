using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddWebToolsCore_RegistersWebAccessService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddWebToolsCore();
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<IWebAccessService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<WebAccessService>();
    }

    [Fact]
    public void AddWebToolsCore_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWebToolsCore();
        var provider = services.BuildServiceProvider();

        // Act
        var first = provider.GetRequiredService<IWebAccessService>();
        var second = provider.GetRequiredService<IWebAccessService>();

        // Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task AddBrowserServices_WithPlaywright_RegistersLegacyServicesAndSessionFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrowserServices(EBrowserEngine.Playwright);
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IBrowserSessionFactory>();
        var legacyFactory = provider.GetRequiredService<IBrowserAgentSessionFactory>();
        var interaction = provider.GetRequiredService<IBrowserInteraction>();
        var legacyInteraction = provider.GetRequiredService<IBrowserAgentInteraction>();
        var session = factory.Create();
        var legacySession = legacyFactory.Create();

        // Assert
        provider.GetService<IWebContentFetcher>().Should().NotBeNull();
        provider.GetService<IWebSearchProvider>().Should().NotBeNull();
        legacyFactory.Should().BeSameAs(factory);
        legacyInteraction.Should().BeSameAs(interaction);
        provider.GetService<IBrowserSession>().Should().BeNull();
        provider.GetService<BrowserSession>().Should().BeNull();
        session.Should().BeOfType<PlaywrightSession>();
        legacySession.Should().BeOfType<PlaywrightSession>();

        await session.DisposeAsync();
        await legacySession.DisposeAsync();
    }

    [Fact]
    public async Task AddBrowserServices_WithCloakBrowser_RegistersLegacyServicesAndSessionFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrowserServices(EBrowserEngine.CloakBrowser);
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IBrowserSessionFactory>();
        var legacyFactory = provider.GetRequiredService<IBrowserAgentSessionFactory>();
        var interaction = provider.GetRequiredService<IBrowserInteraction>();
        var legacyInteraction = provider.GetRequiredService<IBrowserAgentInteraction>();
        var session = factory.Create();
        var legacySession = legacyFactory.Create();

        // Assert
        provider.GetService<IWebContentFetcher>().Should().NotBeNull();
        provider.GetService<IWebSearchProvider>().Should().NotBeNull();
        legacyFactory.Should().BeSameAs(factory);
        legacyInteraction.Should().BeSameAs(interaction);
        provider.GetService<IBrowserSession>().Should().BeNull();
        provider.GetService<BrowserSession>().Should().BeNull();
        session.Should().BeOfType<CloakBrowserSession>();
        legacySession.Should().BeOfType<CloakBrowserSession>();

        await session.DisposeAsync();
        await legacySession.DisposeAsync();
    }

    [Fact]
    public async Task SessionFactory_CreatesIndependentSessionForEachCall()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrowserServices(EBrowserEngine.Playwright);
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IBrowserSessionFactory>();

        // Act
        var first = factory.Create();
        var second = factory.Create();

        // Assert
        first.Should().NotBeSameAs(second);
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task AddBrowserServices_AppliesBrowserSessionOptionsToFactorySessions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrowserServices(
            EBrowserEngine.Playwright,
            browserSessionOptions: new BrowserSessionOptions { ViewportHeight = 720 });
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IBrowserSessionFactory>();
        var session = factory.Create();

        // Act
        var height = await session.GetViewportHeightAsync();

        // Assert
        height.Should().Be(720);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task AddBrowserServices_LegacyOptionsOverload_MapsNestedSessionOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrowserServices(new BrowserAgentOptions
        {
            SessionOptions = new BrowserSessionOptions { ViewportHeight = 720 }
        });
        await using var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<IBrowserAgentSessionFactory>().Create();
        var height = await session.GetViewportHeightAsync();

        // Assert
        height.Should().Be(720);
        await session.DisposeAsync();
    }

    [Fact]
    public void AddBrowserServices_LegacyNullOptionsOverload_IsAccepted()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrowserServices(null);
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IBrowserAgentSessionFactory>().Should().NotBeNull();
        provider.GetService<IBrowserSessionFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddBrowserServices_DoesNotOverrideExistingRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWebToolsCore();

        // Act — register twice
        services.AddBrowserServices(EBrowserEngine.Playwright);
        services.AddBrowserServices(EBrowserEngine.CloakBrowser);
        var provider = services.BuildServiceProvider();

        // Assert — first registration wins (TryAdd semantics)
        var fetcher = provider.GetRequiredService<IWebContentFetcher>();
        fetcher.GetType().Name.Should().Contain("Playwright");
    }
}
