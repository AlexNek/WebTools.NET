using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using WebTools.NET.Abstractions;

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
    public void AddBrowserServices_WithPlaywright_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrowserServices(EBrowserEngine.Playwright);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IWebContentFetcher>().Should().NotBeNull();
        provider.GetService<IWebSearchProvider>().Should().NotBeNull();
        provider.GetService<IBrowserInteraction>().Should().NotBeNull();
    }

    [Fact]
    public void AddBrowserServices_WithCloakBrowser_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBrowserServices(EBrowserEngine.CloakBrowser);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IWebContentFetcher>().Should().NotBeNull();
        provider.GetService<IWebSearchProvider>().Should().NotBeNull();
        provider.GetService<IBrowserInteraction>().Should().NotBeNull();
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
