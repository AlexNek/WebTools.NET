using FluentAssertions;

using WebTools.NET;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class WebAccessServiceTests : IDisposable
{
    private readonly WebAccessService _sut = new();

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task CheckReachabilityAsync_ReachableUrl_ReturnsSuccess()
    {
        // Arrange
        var url = "https://httpbin.org/status/200";

        // Act
        var result = await _sut.CheckReachabilityAsync(url);

        // Assert
        result.Reachable.Should().BeTrue();
        result.HttpStatus.Should().Be(200);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckReachabilityAsync_NotFoundUrl_ReturnsNotReachable()
    {
        // Arrange
        var url = "https://httpbin.org/status/404";

        // Act
        var result = await _sut.CheckReachabilityAsync(url);

        // Assert
        result.Reachable.Should().BeFalse();
        result.HttpStatus.Should().Be(404);
        result.ErrorMessage.Should().Contain("404");
    }

    [Fact]
    public async Task CheckReachabilityAsync_RedirectUrl_FollowsRedirectAndReportsCount()
    {
        // Arrange
        var url = "https://httpbin.org/redirect/2";

        // Act
        var result = await _sut.CheckReachabilityAsync(url);

        // Assert
        result.Reachable.Should().BeTrue();
        result.RedirectCount.Should().Be(2);
        result.FinalUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CheckReachabilityAsync_InvalidDomain_ReturnsErrorMessage()
    {
        // Arrange
        var url = "https://this-domain-definitely-does-not-exist-xyz999.com";

        // Act
        var result = await _sut.CheckReachabilityAsync(url);

        // Assert
        result.Reachable.Should().BeFalse();
        result.HttpStatus.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CheckReachabilityAsync_CancelledToken_ThrowsOrReturnsTimeout()
    {
        // Arrange
        var url = "https://httpbin.org/delay/10";
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var result = await _sut.CheckReachabilityAsync(url, cts.Token);

        // Assert
        result.Reachable.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
