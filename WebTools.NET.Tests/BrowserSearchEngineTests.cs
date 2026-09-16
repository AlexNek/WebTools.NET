using FluentAssertions;
using NSubstitute;

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
    public async Task SearchAsync_WhenCanceledAfterBothPrimaryAttempts_DoesNotInvokeFallback()
    {
        // Arrange
        var primaryCalls = 0;
        var primaryEvaluations = 0;
        var fallbackCalls = 0;
        using var cts = new CancellationTokenSource();
        var primaryPage = CreateBlockedPage("captcha");
        primaryPage.EvaluateAsync<string>(Arg.Any<string>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref primaryEvaluations) == 2)
                {
                    cts.Cancel();
                }

                return Task.FromResult("captcha");
            });
        var sut = CreateEngine(
            _ =>
            {
                primaryCalls++;
                return Task.FromResult<IPage>(primaryPage);
            },
            _ =>
            {
                fallbackCalls++;
                return Task.FromException<ISearchPageLease>(new InvalidOperationException("fallback should not start"));
            });

        // Act
        var result = await sut.SearchAsync("query", 1, cts.Token);

        // Assert
        result.ErrorMessage.Should().Be("Search timed out");
        primaryCalls.Should().Be(1);
        primaryEvaluations.Should().Be(2);
        fallbackCalls.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_WhenBothPrimaryAttemptsAreBlocked_InvokesFallbackAndDisposesLease()
    {
        // Arrange
        var primaryPage = CreateBlockedPage("captcha");
        var fallbackPage = CreateBlockedPage("access denied");
        var fallbackCalls = 0;
        var resourceDisposals = 0;
        var ownershipReleases = 0;
        var fallbackLease = new SearchPageLease(
            fallbackPage,
            () =>
            {
                resourceDisposals++;
                return ValueTask.CompletedTask;
            },
            () =>
            {
                ownershipReleases++;
                return ValueTask.CompletedTask;
            });
        var sut = CreateEngine(
            _ => Task.FromResult(primaryPage),
            _ =>
            {
                fallbackCalls++;
                return Task.FromResult<ISearchPageLease>(fallbackLease);
            });

        // Act
        var result = await sut.SearchAsync("query", 1, CancellationToken.None);

        // Assert
        fallbackCalls.Should().Be(1);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Non-headless fallback");
        resourceDisposals.Should().Be(1);
        ownershipReleases.Should().Be(1);
        await fallbackPage.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task SearchAsync_WhenPrimaryAttemptIsNotBlocked_DoesNotInvokeFallback()
    {
        // Arrange
        var primaryPage = CreateBlockedPage(string.Empty);
        var fallbackCalls = 0;
        var sut = CreateEngine(
            _ => Task.FromResult(primaryPage),
            _ =>
            {
                fallbackCalls++;
                return Task.FromException<ISearchPageLease>(new InvalidOperationException("fallback should not start"));
            });

        // Act
        var result = await sut.SearchAsync("query", 1, CancellationToken.None);

        // Assert
        fallbackCalls.Should().Be(0);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotContain("Non-headless fallback");
    }

    [Fact]
    public async Task SearchAsync_WhenFallbackReturnsResults_ReturnsFallbackResult()
    {
        // Arrange
        var primaryPage = CreateBlockedPage("captcha");
        var fallbackPage = CreateSuccessfulBingPage();
        var fallbackLease = new SearchPageLease(
            fallbackPage,
            () => ValueTask.CompletedTask,
            () => ValueTask.CompletedTask);
        var sut = CreateEngine(
            _ => Task.FromResult(primaryPage),
            _ => Task.FromResult<ISearchPageLease>(fallbackLease));

        // Act
        var result = await sut.SearchAsync("query", 1, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Results.Should().ContainSingle();
        result.Results[0].Title.Should().Be("Fallback result");
        result.Results[0].Url.Should().Be("https://test.example.com/result");
    }

    [Fact]
    public async Task SearchAsync_WhenCanceledBeforeFallback_DoesNotInvokeFallback()
    {
        // Arrange
        var primaryPage = CreateBlockedPage("captcha");
        var fallbackCalls = 0;
        using var cts = new CancellationTokenSource();
        var sut = CreateEngine(
            _ => Task.FromResult(primaryPage),
            _ =>
            {
                fallbackCalls++;
                return Task.FromException<ISearchPageLease>(new InvalidOperationException("fallback should not start"));
            });

        cts.Cancel();

        // Act
        var result = await sut.SearchAsync("query", 1, cts.Token);

        // Assert
        fallbackCalls.Should().Be(0);
        result.ErrorMessage.Should().Be("Search timed out");
    }

    private static BrowserSearchEngine CreateEngine(
        Func<CancellationToken, Task<IPage>> getPageAsync,
        Func<CancellationToken, Task<ISearchPageLease>> getFallbackPageLeaseAsync) =>
        new(
            getPageAsync,
            logger: null,
            engineName: "Test",
            getFallbackPageLeaseAsync,
            delayAsync: (_, cancellationToken) => cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask);

    private static IPage CreateBlockedPage(string marker)
    {
        var page = Substitute.For<IPage>();
        var locator = Substitute.For<ILocator>();
        page.Locator(Arg.Any<string>()).Returns(locator);
        locator.WaitForAsync(Arg.Any<LocatorWaitForOptions>())
            .Returns(Task.FromException(new TimeoutException("selector unavailable")));
        page.EvaluateAsync<string>(Arg.Any<string>())
            .Returns(Task.FromResult(marker));
        page.DisposeAsync().Returns(ValueTask.CompletedTask);
        return page;
    }

    private static IPage CreateSuccessfulBingPage()
    {
        var page = Substitute.For<IPage>();
        var locator = Substitute.For<ILocator>();
        var keyboard = Substitute.For<IKeyboard>();
        var jsHandle = Substitute.For<IJSHandle>();
        page.Locator(Arg.Any<string>()).Returns(locator);
        locator.WaitForAsync(Arg.Any<LocatorWaitForOptions>()).Returns(Task.CompletedTask);
        locator.ClickAsync().Returns(Task.CompletedTask);
        page.Keyboard.Returns(keyboard);
        keyboard.TypeAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
        keyboard.PressAsync("Enter").Returns(Task.CompletedTask);
        page.Url.Returns("https://www.bing.com/search?q=query");
        page.WaitForFunctionAsync(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<PageWaitForFunctionOptions>())
            .Returns(Task.FromResult(jsHandle));
        page.WaitForSelectorAsync(
                Arg.Any<string>(),
                Arg.Any<PageWaitForSelectorOptions>())
            .Returns(Task.FromResult<IElementHandle?>(null));
        page.EvaluateAsync<string>(Arg.Any<string>())
            .Returns(Task.FromResult("""[{"title":"Fallback result","url":"https://test.example.com/result","snippet":"snippet"}]"""));
        page.DisposeAsync().Returns(ValueTask.CompletedTask);
        return page;
    }

    private static SearchAttemptOutcome Outcome(string message, SearchFailureKind failureKind)
    {
        return new SearchAttemptOutcome(
            new SearchResult(false, [], message),
            failureKind);
    }
}
