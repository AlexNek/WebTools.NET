using FluentAssertions;

using NSubstitute;

using WebTools.NET.Abstractions;

using Xunit;

namespace WebTools.NET.Tests;

public class WebNavigationAgentTests
{
    [Fact]
    public async Task NavigateAsync_WhenBrowserFails_ReturnsEmptyList()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        browser.When(x => x.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Connection refused"));

        var sut = new WebNavigationAgent(browser);

        // Act
        var result = await sut.NavigateAsync("https://example.com");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NavigateAsync_WhenPageHasLinks_ExtractsAbsoluteUrls()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns("""
                <html><body>
                    <a href="/page1">Page 1</a>
                    <a href="https://example.com/page2">Page 2</a>
                    <a href="https://other.com/external">External</a>
                    <a href="javascript:void(0)">JS Link</a>
                    <a href="mailto:test@test.com">Email</a>
                </body></html>
                """);
        browser.CheckReachabilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new WebNavigationAgent(browser);

        // Act
        var result = await sut.NavigateAsync("https://example.com");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(url => url.Should().StartWith("https://example.com"));
        result.Should().NotContain(url => url.Contains("javascript:"));
        result.Should().NotContain(url => url.Contains("mailto:"));
        result.Should().NotContain(url => url.Contains("other.com"));
    }

    [Fact]
    public async Task NavigateAsync_RespectsMaxLinksLimit()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var links = string.Join("", Enumerable.Range(1, 50)
            .Select(i => $"""<a href="/page{i}">Page {i}</a>"""));
        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns($"<html><body>{links}</body></html>");
        browser.CheckReachabilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new WebNavigationAgent(browser);

        // Act
        var result = await sut.NavigateAsync("https://example.com", maxLinks: 5);

        // Assert
        result.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task ClickAndExtractAsync_WhenClickFails_ReturnsEmptyList()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        browser.When(x => x.ClickAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Element not found"));

        var sut = new WebNavigationAgent(browser);

        // Act
        var result = await sut.ClickAndExtractAsync("button.next");

        // Assert
        result.Should().BeEmpty();
    }
}
