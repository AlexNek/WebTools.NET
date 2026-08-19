using System.Net;

using FluentAssertions;

using WebTools.NET.Geo;

using Xunit;

namespace WebTools.NET.Tests;

public class GeoRegionServiceTests
{
    [Fact]
    public async Task DetectRegionAsync_WhenApiReturnsUS_ReturnsUs()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"countryCode":"US"}""");
        var httpClient = new HttpClient(handler);
        var sut = new GeoRegionService(httpClient);

        // Act
        var result = await sut.DetectRegionAsync();

        // Assert
        result.Should().Be("us");
    }

    [Fact]
    public async Task DetectRegionAsync_WhenApiReturnsCN_ReturnsChina()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"countryCode":"CN"}""");
        var httpClient = new HttpClient(handler);
        var sut = new GeoRegionService(httpClient);

        // Act
        var result = await sut.DetectRegionAsync();

        // Assert
        result.Should().Be("china");
    }

    [Fact]
    public async Task DetectRegionAsync_WhenApiReturnsDE_ReturnsEu()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"countryCode":"DE"}""");
        var httpClient = new HttpClient(handler);
        var sut = new GeoRegionService(httpClient);

        // Act
        var result = await sut.DetectRegionAsync();

        // Assert
        result.Should().Be("eu");
    }

    [Fact]
    public async Task DetectRegionAsync_WhenApiReturnsUnknownCountry_ReturnsIntl()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"countryCode":"BR"}""");
        var httpClient = new HttpClient(handler);
        var sut = new GeoRegionService(httpClient);

        // Act
        var result = await sut.DetectRegionAsync();

        // Assert
        result.Should().Be("intl");
    }

    [Fact]
    public async Task DetectRegionAsync_WhenApiFails_FallsBackToLocale()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var httpClient = new HttpClient(handler);
        var sut = new GeoRegionService(httpClient);

        // Act
        var result = await sut.DetectRegionAsync();

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().BeOneOf("us", "eu", "china", "intl");
    }

    [Fact]
    public async Task DetectRegionAsync_CachesResult_OnSubsequentCalls()
    {
        // Arrange
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"countryCode":"US"}""")
            };
        });
        var httpClient = new HttpClient(handler);
        var sut = new GeoRegionService(httpClient);

        // Act
        var first = await sut.DetectRegionAsync();
        var second = await sut.DetectRegionAsync();

        // Assert
        first.Should().Be("us");
        second.Should().Be("us");
        callCount.Should().Be(1, "result should be cached after first call");
    }
}
