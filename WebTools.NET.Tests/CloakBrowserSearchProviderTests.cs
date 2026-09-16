using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using WebTools.NET.Abstractions;
using WebTools.NET.Search;

using Xunit;

namespace WebTools.NET.Tests;

public class CloakBrowserSearchProviderTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public async Task Constructor_ConfiguresVisibleFallback(
        bool headless,
        bool enableVisibleSearchFallback,
        bool expected)
    {
        // Arrange
        await using var sut = new CloakBrowserSearchProvider(
            headless: headless,
            enableVisibleSearchFallback: enableVisibleSearchFallback);

        // Act
        var actual = sut.VisibleFallbackEnabled;

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task AddBrowserServices_DefaultsVisibleFallbackToDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrowserServices(EBrowserEngine.CloakBrowser);
        await using var provider = services.BuildServiceProvider();

        // Act
        var searchProvider = provider.GetRequiredService<IWebSearchProvider>()
            .Should().BeOfType<CloakBrowserSearchProvider>().Subject;

        // Assert
        searchProvider.VisibleFallbackEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task AddBrowserServices_EnablesVisibleFallbackWhenRequested()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrowserServices(
            EBrowserEngine.CloakBrowser,
            enableVisibleSearchFallback: true);
        await using var provider = services.BuildServiceProvider();

        // Act
        var searchProvider = provider.GetRequiredService<IWebSearchProvider>()
            .Should().BeOfType<CloakBrowserSearchProvider>().Subject;

        // Assert
        searchProvider.VisibleFallbackEnabled.Should().BeTrue();
    }
}
