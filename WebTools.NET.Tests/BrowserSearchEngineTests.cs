using FluentAssertions;

using Microsoft.Playwright;

using WebTools.NET.Models;
using WebTools.NET.Search;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserSearchEngineTests
{
    [Fact]
    public void ShouldUseFallback_WhenBothEngineOutcomesAreBlocked_ReturnsTrue()
    {
        // Arrange
        var bing = Outcome("Bing blocked request", SearchFailureKind.Blocked);
        var duckDuckGo = Outcome("Search engine blocked the request", SearchFailureKind.Blocked);

        // Act
        var result = BrowserSearchEngine.ShouldUseFallback(bing, duckDuckGo, fallbackAvailable: true);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData((int)SearchFailureKind.Timeout, (int)SearchFailureKind.Blocked)]
    [InlineData((int)SearchFailureKind.NoResults, (int)SearchFailureKind.Blocked)]
    [InlineData((int)SearchFailureKind.Blocked, (int)SearchFailureKind.Timeout)]
    [InlineData((int)SearchFailureKind.Blocked, (int)SearchFailureKind.NoResults)]
    [InlineData((int)SearchFailureKind.Other, (int)SearchFailureKind.Other)]
    public void ShouldUseFallback_WhenEitherOutcomeIsNotBlocked_ReturnsFalse(
        int bingFailure,
        int duckDuckGoFailure)
    {
        // Arrange
        var bing = Outcome("Bing failure", (SearchFailureKind)bingFailure);
        var duckDuckGo = Outcome("DuckDuckGo failure", (SearchFailureKind)duckDuckGoFailure);

        // Act
        var result = BrowserSearchEngine.ShouldUseFallback(bing, duckDuckGo, fallbackAvailable: true);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldUseFallback_WhenFallbackIsUnavailable_ReturnsFalse()
    {
        // Arrange
        var bing = Outcome("Bing blocked request", SearchFailureKind.Blocked);
        var duckDuckGo = Outcome("Search engine blocked the request", SearchFailureKind.Blocked);

        // Act
        var result = BrowserSearchEngine.ShouldUseFallback(bing, duckDuckGo, fallbackAvailable: false);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldUseFallback_DoesNotInspectCombinedErrorText()
    {
        // Arrange
        var bing = Outcome("Bing blocked request; DuckDuckGo fallback: timeout", SearchFailureKind.Other);
        var duckDuckGo = Outcome("No DuckDuckGo results", SearchFailureKind.NoResults);

        // Act
        var result = BrowserSearchEngine.ShouldUseFallback(bing, duckDuckGo, fallbackAvailable: true);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldUseFallback_WhenAnOutcomeIsMissing_ReturnsFalse()
    {
        // Arrange
        var bing = Outcome("Bing blocked request", SearchFailureKind.Blocked);

        // Act
        var result = BrowserSearchEngine.ShouldUseFallback(bing, null, fallbackAvailable: true);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_WhenCanceledBeforePrimaryAttempt_DoesNotInvokeFallback()
    {
        // Arrange
        var primaryCalls = 0;
        var fallbackCalls = 0;
        var sut = new BrowserSearchEngine(
            _ =>
            {
                primaryCalls++;
                return Task.FromException<IPage>(new InvalidOperationException("primary should not start"));
            },
            logger: null,
            engineName: "Test",
            _ =>
            {
                fallbackCalls++;
                return Task.FromException<ISearchPageLease>(new InvalidOperationException("fallback should not start"));
            });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await sut.SearchAsync("query", 1, cts.Token);

        // Assert
        result.ErrorMessage.Should().Be("Search timed out");
        primaryCalls.Should().Be(0);
        fallbackCalls.Should().Be(0);
    }

    private static SearchAttemptOutcome Outcome(string message, SearchFailureKind failureKind)
    {
        return new SearchAttemptOutcome(
            new SearchResult(false, [], message),
            failureKind);
    }
}
