using FluentAssertions;

using WebTools.NET.Browsing;
using WebTools.NET.Search;

using Xunit;

namespace WebTools.NET.Tests;

/// <summary>
/// Live-network integration tests. These hit the real internet and require
/// installed Playwright browsers; CI excludes them via the Category=Integration
/// trait filter. Unit tests must stay hermetic — do not remove the trait.
/// </summary>
[Trait("Category", "Integration")]
public class BrowserIntegrationTests
{
    [Fact]
    public async Task PlaywrightContentFetcher_FetchesRealPage()
    {
        // Arrange
        await using var fetcher = new PlaywrightContentFetcher();

        // Act
        var result = await fetcher.FetchAsync("https://example.com");

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlaywrightSearchProvider_CompletesSearchRoundTrip()
    {
        // Arrange
        await using var provider = new PlaywrightSearchProvider(headless: true);

        // Act
        var result = await provider.SearchAsync(".NET 10 new features", maxResults: 1);

        // Assert — live engines can block requests; only the round trip is asserted
        result.Should().NotBeNull();
        if (result.Success)
        {
            result.Results.Should().NotBeEmpty();
        }
        else
        {
            result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        }
    }
}
