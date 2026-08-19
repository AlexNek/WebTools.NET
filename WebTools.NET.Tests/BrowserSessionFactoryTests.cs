using FluentAssertions;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserSessionFactoryTests
{
    [Fact]
    public async Task Create_Playwright_ReturnsFreshPlaywrightSession()
    {
        // Arrange
        var sut = new BrowserSessionFactory(EBrowserEngine.Playwright);

        // Act
        var first = sut.Create();
        var second = sut.Create();

        // Assert
        first.Should().BeOfType<PlaywrightSession>();
        second.Should().BeOfType<PlaywrightSession>();
        first.Should().NotBeSameAs(second);
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task Create_CloakBrowser_ReturnsCloakBrowserSession()
    {
        // Arrange
        var sut = new BrowserSessionFactory(EBrowserEngine.CloakBrowser);

        // Act
        var session = sut.Create();

        // Assert
        session.Should().BeOfType<CloakBrowserSession>();
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Create_AppliesSessionOptions()
    {
        // Arrange
        var sut = new BrowserSessionFactory(
            sessionOptions: new BrowserSessionOptions { ViewportHeight = 720 });

        // Act
        var session = sut.Create();

        // Assert
        (await session.GetViewportHeightAsync()).Should().Be(720);
        await session.DisposeAsync();
    }

    [Fact]
    public void Constructor_WhenEngineIsUnknown_Throws()
    {
        // Arrange
        var act = () => new BrowserSessionFactory((EBrowserEngine)999);

        // Act / Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("engine");
    }
}
