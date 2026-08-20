using FluentAssertions;

using WebTools.NET.Internal;

using Xunit;

namespace WebTools.NET.Tests;

public class HttpStatusHelperTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(304)]
    [InlineData(307)]
    [InlineData(308)]
    public void IsSuccessOrRedirect_AcceptsReachableStatus(int status)
    {
        // Act
        var result = HttpStatusHelper.IsSuccessOrRedirect(status);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(199)]
    [InlineData(400)]
    [InlineData(500)]
    public void IsSuccessOrRedirect_RejectsUnreachableStatus(int status)
    {
        // Act
        var result = HttpStatusHelper.IsSuccessOrRedirect(status);

        // Assert
        result.Should().BeFalse();
    }
}
