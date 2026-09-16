using FluentAssertions;

using Microsoft.Playwright;

using NSubstitute;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserContentFetcherBaseTests
{
    [Fact]
    public void CreateResult_MarkdownWithAbsoluteUrls_UsesFinalUrlAsBase()
    {
        // Arrange
        var rawBody = "<a href=\"details\">Details</a>";
        var finalUrl = "https://test.example.com/docs/";

        // Act
        var result = TestBrowserContentFetcher.CreateResult(
            rawBody,
            finalUrl,
            200,
            WebTools.NET.Models.EContentFormat.MarkdownWithAbsoluteUrls,
            null,
            WebTools.NET.Models.ESanitizeLevel.Strict);

        // Assert
        result.Success.Should().BeTrue();
        result.FinalUrl.Should().Be(finalUrl);
        result.Content.Should().Contain("https://test.example.com/docs/details");
    }

    [Fact]
    public async Task FetchAsync_WhenNavigationTimesOut_ReportsNullFinalUrl()
    {
        // Arrange
        await using var sut = new TimeoutBrowserContentFetcher();

        // Act
        var result = await sut.FetchAsync("https://test.example.com");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Request timed out");
        result.FinalUrl.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenBodyReadFailsAfterNavigationCompletes_PreservesLandedFinalUrl()
    {
        // Arrange: navigation completes and reports a landed URL, then the subsequent body read
        // throws a PlaywrightException. The result must retain the landed URL, not null.
        const string landedUrl = "https://test.example.com/landed";

        var response = Substitute.For<IResponse>();
        response.Url.Returns(landedUrl);
        response.Status.Returns(200);

        var page = Substitute.For<IPage>();
        page.Url.Returns(landedUrl);
        page.GotoAsync(Arg.Any<string>(), Arg.Any<PageGotoOptions>()).Returns(response);
        page.WaitForLoadStateAsync(Arg.Any<LoadState>(), Arg.Any<PageWaitForLoadStateOptions>())
            .Returns(Task.CompletedTask);
        // The body read is the post-navigation step that fails.
        page.TextContentAsync("body", Arg.Any<PageTextContentOptions>())
            .Returns<string?>(_ => throw new PlaywrightException("Body read failed."));
        page.CloseAsync().Returns(Task.CompletedTask);

        var context = Substitute.For<IBrowserContext>();
        context.NewPageAsync().Returns(page);
        context.CloseAsync().Returns(Task.CompletedTask);

        await using var sut = new MockContextBrowserContentFetcher(context);

        // Act
        var result = await sut.FetchAsync("https://test.example.com/requested");

        // Assert
        result.Success.Should().BeFalse();
        result.FinalUrl.Should().Be(landedUrl);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForAnActiveOperationBeforeDisposingResources()
    {
        // Arrange
        await using var sut = new TestBrowserContentFetcher();
        var fetch = sut.FetchAsync("https://test.example.com");
        await sut.ContextCreationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Act
        var dispose = sut.DisposeAsync().AsTask();
        await Task.Delay(TimeSpan.FromSeconds(5.2));

        // Assert
        dispose.IsCompleted.Should().BeFalse();
        sut.ResourcesDisposed.Should().BeFalse();

        sut.ReleaseContextCreation.SetResult();
        Func<Task> act = async () => await fetch;
        await act.Should().ThrowAsync<InvalidOperationException>();
        await dispose;
        sut.ResourcesDisposed.Should().BeTrue();
    }
}
