using System.Net;

using FluentAssertions;

using WebTools.NET.Models;
using WebTools.NET.Search;

using Xunit;

namespace WebTools.NET.Tests;

public class DuckDuckGoSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        using var sut = new DuckDuckGoSearchProvider();

        // Act
        var result = await sut.SearchAsync("dotnet", maxResults: 3);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().NotBeEmpty();
        result.Results.Should().AllSatisfy(item =>
        {
            item.Title.Should().NotBeNullOrWhiteSpace();
            item.Url.Should().StartWith("http");
        });
    }

    [Fact]
    public async Task SearchAsync_WithMaxResults_RespectsLimit()
    {
        // Arrange
        using var sut = new DuckDuckGoSearchProvider();
        const int maxResults = 2;

        // Act
        var result = await sut.SearchAsync("C# programming", maxResults: maxResults);

        // Assert
        result.Results.Count.Should().BeLessThanOrEqualTo(maxResults);
    }

    [Fact]
    public async Task SearchAsync_WithHttpClientFailure_ReturnsFailedResult()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var httpClient = new HttpClient(fakeHandler)
        {
            BaseAddress = new Uri("https://html.duckduckgo.com")
        };
        using var sut = new DuckDuckGoSearchProvider(httpClient);

        // Act
        var result = await sut.SearchAsync("test query");

        // Assert
        result.Success.Should().BeFalse();
        result.Results.Should().BeEmpty();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SearchAsync_WithEmptyHtml_ReturnsEmptyResults()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, "<html><body></body></html>");
        var httpClient = new HttpClient(fakeHandler);
        using var sut = new DuckDuckGoSearchProvider(httpClient);

        // Act
        var result = await sut.SearchAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().BeEmpty();
    }
}
