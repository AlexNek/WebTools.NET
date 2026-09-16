using FluentAssertions;

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
}
