using FluentAssertions;

using Microsoft.Playwright;

using WebTools.NET.Internal;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserHelpersTests
{
    [Fact]
    public void NormalizeStatusAfterChallenge_WhenResolved_Returns200()
    {
        // Act
        var status = BrowserHelpers.NormalizeStatusAfterChallenge(403, challengeResolved: true);

        // Assert — the original 403 was the challenge interstitial, not the page
        status.Should().Be(200);
    }

    [Fact]
    public void NormalizeStatusAfterChallenge_WhenNotResolved_KeepsOriginalStatus()
    {
        // Act
        var status = BrowserHelpers.NormalizeStatusAfterChallenge(403, challengeResolved: false);

        // Assert
        status.Should().Be(403);
    }

    [Fact]
    public void NormalizePlaywrightError_WhenExecutableMissing_ReturnsInstallHint()
    {
        // Arrange
        var ex = new PlaywrightException("Executable doesn't exist at some/path");

        // Act
        var message = BrowserHelpers.NormalizePlaywrightError(ex, "install hint");

        // Assert
        message.Should().Be("install hint");
    }

    [Fact]
    public void NormalizePlaywrightError_WhenOtherError_ReturnsOriginalMessage()
    {
        // Arrange
        var ex = new PlaywrightException("some other failure");

        // Act
        var message = BrowserHelpers.NormalizePlaywrightError(ex, "install hint");

        // Assert
        message.Should().Be("some other failure");
    }
}
