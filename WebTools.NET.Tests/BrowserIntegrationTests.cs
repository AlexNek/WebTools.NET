using FluentAssertions;

using WebTools.NET.Search;

using Xunit;

namespace WebTools.NET.Tests;

/// <summary>
/// Playwright integration tests that access live network services. Local browser
/// regression tests are kept in PlaywrightBrowserIntegrationTests so CI can run them
/// without enabling live-network tests.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveNetwork")]
public class BrowserIntegrationTests
{
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
