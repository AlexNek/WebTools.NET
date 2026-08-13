using FluentAssertions;

using WebTools.NET.Internal;

using Xunit;

namespace WebTools.NET.Tests;

public class StealthScriptsTests
{
    [Fact]
    public void ForMode_WhenHeadless_ReturnsFullStealthScript()
    {
        // Act
        var script = StealthScripts.ForMode(headless: true);

        // Assert — full stealth covers the common detection vectors
        script.Should().Contain("webdriver");
        script.Should().Contain("WebGLRenderingContext");
        script.Should().Contain("plugins");
        script.Should().NotBe(StealthScripts.Minimal);
    }

    [Fact]
    public void ForMode_WhenNotHeadless_ReturnsMinimalStealthScript()
    {
        // Act
        var script = StealthScripts.ForMode(headless: false);

        // Assert — a visible browser only needs the webdriver override
        script.Should().Be(StealthScripts.Minimal);
        script.Should().Contain("webdriver");
    }
}
