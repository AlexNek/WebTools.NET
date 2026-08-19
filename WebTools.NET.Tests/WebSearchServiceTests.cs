using FluentAssertions;

using NSubstitute;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class WebSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenProviderReturnsResults_ReturnsThemDirectly()
    {
        // Arrange
        var searchProvider = Substitute.For<IWebSearchProvider>();
        var expected = new SearchResult(true, [new SearchResultItem("Title", "https://test.example.com", "Snippet")], null);
        searchProvider.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var sut = new WebSearchService(searchProvider);

        // Act
        var result = await sut.SearchAsync("test query");

        // Assert
        result.Should().BeSameAs(expected);
        await searchProvider.Received(1).SearchAsync("test query", 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WhenFirstSearchReturnsEmpty_TriesFallbackQueries()
    {
        // Arrange
        var searchProvider = Substitute.For<IWebSearchProvider>();
        var emptyResult = new SearchResult(true, [], null);
        var fallbackResult = new SearchResult(true, [new SearchResultItem("Fallback", "https://test.example.com", "Found")], null);

        searchProvider.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyResult, fallbackResult);

        var sut = new WebSearchService(searchProvider);

        // Act
        var result = await sut.SearchAsync("dotnet");

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(1);
        result.Results[0].Title.Should().Be("Fallback");
    }

    [Fact]
    public async Task SearchAsync_WhenQueryContainsAPI_GeneratesAppropriateFallbacks()
    {
        // Arrange
        var searchProvider = Substitute.For<IWebSearchProvider>();
        var emptyResult = new SearchResult(true, [], null);
        var found = new SearchResult(true, [new SearchResultItem("Found", "https://test.example.com", "")], null);

        searchProvider.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyResult, emptyResult, found);

        var sut = new WebSearchService(searchProvider);

        // Act
        var result = await sut.SearchAsync("Playwright API");

        // Assert
        result.Success.Should().BeTrue();
        // Should have tried: original "Playwright API", then "Playwright", then "Playwright API official site"
        await searchProvider.ReceivedWithAnyArgs(3).SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task SearchAsync_RespectsMaxResults()
    {
        // Arrange
        var searchProvider = Substitute.For<IWebSearchProvider>();
        var expected = new SearchResult(true, [new SearchResultItem("T", "https://x.com", "S")], null);
        searchProvider.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var sut = new WebSearchService(searchProvider);

        // Act
        await sut.SearchAsync("query", maxResults: 7);

        // Assert
        await searchProvider.Received(1).SearchAsync("query", 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WhenAllSearchesReturnEmpty_DoesNotRepeatOriginalQuery()
    {
        // Arrange
        var searchProvider = Substitute.For<IWebSearchProvider>();
        var emptyResult = new SearchResult(true, [], null);
        searchProvider.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyResult);

        var sut = new WebSearchService(searchProvider);

        // Act
        await sut.SearchAsync("Playwright .NET API");

        // Assert — original + 2 fallbacks, original never repeated
        await searchProvider.ReceivedWithAnyArgs(3).SearchAsync(default!, default, default);
        await searchProvider.Received(1)
            .SearchAsync("Playwright .NET API", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
