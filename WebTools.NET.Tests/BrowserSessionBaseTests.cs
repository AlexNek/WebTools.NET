using FluentAssertions;

using WebTools.NET.Browsing;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserSessionBaseTests
{
    [Fact]
    public async Task ScreenshotAsync_WhenScopeIsUnsupported_Throws()
    {
        // Arrange
        await using var sut = new PlaywrightSession();

        // Act
        var act = () => sut.ScreenshotAsync((EScreenshotScope)99);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        exception.Which.ParamName.Should().Be("scope");
    }
}
