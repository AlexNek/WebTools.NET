using FluentAssertions;

using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class UrlCheckResultTests
{
    [Fact]
    public void UrlCheckResult_WithRedirects_TracksCount()
    {
        // Arrange & Act
        var result = new UrlCheckResult(
            Reachable: true,
            HttpStatus: 200,
            ErrorMessage: null,
            RedirectCount: 3,
            FinalUrl: "https://test.example.com/final")
        {
            ClientRedirectCount = 1
        };

        // Assert
        result.Reachable.Should().BeTrue();
        result.RedirectCount.Should().Be(3);
        result.ClientRedirectCount.Should().Be(1);
        result.FinalUrl.Should().Be("https://test.example.com/final");
        result.ProtectionType.Should().BeNull();
    }

    [Fact]
    public void UrlCheckResult_WithProtection_ReportsType()
    {
        // Arrange & Act
        var result = new UrlCheckResult(
            Reachable: false,
            HttpStatus: 403,
            ErrorMessage: "Blocked",
            ProtectionType: "Cloudflare");

        // Assert
        result.Reachable.Should().BeFalse();
        result.ProtectionType.Should().Be("Cloudflare");
    }
}
