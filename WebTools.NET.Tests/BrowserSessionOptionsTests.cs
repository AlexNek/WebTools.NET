using FluentAssertions;

using WebTools.NET.Browsing;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserSessionOptionsTests
{
    [Fact]
    public async Task PlaywrightSession_UsesConfiguredViewportHeight()
    {
        // Arrange
        await using var session = new PlaywrightSession(
            options: new BrowserSessionOptions { ViewportWidth = 1280, ViewportHeight = 720 });

        // Act
        var height = await session.GetViewportHeightAsync();

        // Assert
        height.Should().Be(720);
    }

    [Fact]
    public void ViewportWidth_WhenNotPositive_Throws()
    {
        // Arrange
        var act = () => new BrowserSessionOptions { ViewportWidth = 0 };

        // Act / Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("ViewportWidth");
    }

    [Fact]
    public void MaxDuration_WhenBeyondCancellationTokenRange_Throws()
    {
        // Arrange
        var act = () => new BrowserSessionOptions
        {
            MaxDuration = TimeSpan.FromMilliseconds((long)int.MaxValue + 1)
        };

        // Act
        var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;

        // Assert
        exception.ParamName.Should().Be("MaxDuration");
    }
}
