using FluentAssertions;

using NSubstitute;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class WebNavigationServiceTests
{
    [Fact]
    public async Task NavigateAsync_WhenBrowserFails_ReturnsEmptyList()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        browser.When(x => x.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new Exception("Connection refused"));

        var sut = new WebNavigationService(browser);

        // Act
        var result = await sut.NavigateAsync("https://test.example.com");

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
                    <a href="https://test.example.com/page2">Page 2</a>
                    <a href="https://other.example.com/external">External</a>
                    <a href="javascript:void(0)">JS Link</a>
                    <a href="mailto:test@example.test">Email</a>
                </body></html>
                """);
        browser.CheckReachabilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new WebNavigationService(browser);

        // Act
        var result = await sut.NavigateAsync("https://test.example.com");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(url => url.Should().StartWith("https://test.example.com"));
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

        var sut = new WebNavigationService(browser);

        // Act
        var result = await sut.NavigateAsync("https://test.example.com", maxLinks: 5);

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

        var sut = new WebNavigationService(browser);

        // Act
        var result = await sut.ClickAndExtractAsync("button.next");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NavigateAsync_WhenMaxLinksIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        var sut = new WebNavigationService(browser);

        // Act
        var act = () => sut.NavigateAsync("https://test.example.com", maxLinks: -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ClickAndExtractAsync_WhenMaxLinksIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        var sut = new WebNavigationService(browser);

        // Act
        var act = () => sut.ClickAndExtractAsync("button.next", maxLinks: -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ClickAndExtractAsync_WhenBrowserReturnsMalformedUrl_ReturnsEmptyList()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        browser.ClickAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body><a href='/next'>Next</a></body></html>");
        browser.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns("not a URL");
        var sut = new WebNavigationService(browser);

        // Act
        var result = await sut.ClickAndExtractAsync("button.next");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPageElementsAsync_WhenBrowserSupportsAgentCapabilities_ReturnsElements()
    {
        // Arrange
        var browser = Substitute.For<IBrowserSession>();
        IReadOnlyList<InteractiveElement> elements =
        [
            new InteractiveElement(1, "button", "button", "Continue", null, null, "#continue")
        ];
        browser.GetInteractiveElementsAsync(Arg.Any<CancellationToken>()).Returns(elements);
        var sut = new WebNavigationService(browser);

        // Act
        var result = await sut.GetPageElementsAsync();

        // Assert
        result.Should().ContainSingle().Which.Selector.Should().Be("#continue");
    }

    [Fact]
    public async Task GetPageElementsAsync_WhenBrowserLacksAgentCapabilities_ThrowsClearError()
    {
        // Arrange
        var browser = Substitute.For<IBrowserInteraction>();
        var sut = new WebNavigationService(browser);

        // Act
        var act = () => sut.GetPageElementsAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The browser must implement IBrowserElementExtractor to extract page elements.");
    }
}
