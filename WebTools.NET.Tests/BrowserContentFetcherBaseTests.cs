using FluentAssertions;

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
