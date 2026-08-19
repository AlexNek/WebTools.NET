#pragma warning disable CS0618

using FluentAssertions;

using NSubstitute;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserAgentCompatibilityTests
{
    [Fact]
    public async Task BrowserAgent_MapsLegacyActionsAndSnapshotsToSessionImplementation()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserAgent(
            browser,
            new BrowserAgentOptions { MaxActions = 2 });

        // Act
        var started = await sut.StartAsync("https://test.example.com");
        var snapshot = await sut.ExecuteAsync(new BrowserAction(
            EBrowserActionType.Click,
            ElementIndex: 1));

        // Assert
        started.Should().BeOfType<PageSnapshot>();
        started.Url.Should().Be("https://test.example.com/");
        snapshot.Error.Should().BeNull();
        sut.ActionHistory.Should().ContainSingle(action =>
            action.Type == EBrowserActionType.Click);
        await browser.Received().ClickAsync("#submit", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BrowserAgent_MapsLegacyActionLimitToCurrentOperationLimit()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserAgent(
            browser,
            new BrowserAgentOptions { MaxActions = 1 });
        await sut.StartAsync("https://test.example.com");

        // Act
        await sut.ExecuteAsync(new BrowserAction(EBrowserActionType.Snapshot));
        var limited = await sut.ExecuteAsync(new BrowserAction(EBrowserActionType.Snapshot));

        // Assert
        limited.Error.Should().Be("Operation limit (1) reached.");
    }

    [Fact]
    public async Task BrowserAgent_DisposeDoesNotDisposeExternallySuppliedBrowser()
    {
        // Arrange
        var browser = CreateBrowser();
        var sut = new BrowserAgent(browser);

        // Act
        await sut.DisposeAsync();

        // Assert
        await browser.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task BrowserAgentSessionFactory_ReturnsBothLegacyAndCurrentSessionContracts()
    {
        // Arrange
        var sut = new BrowserAgentSessionFactory();

        // Act
        var session = sut.Create();

        // Assert
        session.Should().BeAssignableTo<IBrowserAgentInteraction>();
        session.Should().BeAssignableTo<IBrowserSession>();
        await session.DisposeAsync();
    }

    private static IBrowserAgentInteraction CreateBrowser()
    {
        var browser = Substitute.For<IBrowserAgentInteraction>();
        browser.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns("https://test.example.com/");
        browser.GetTitleAsync(Arg.Any<CancellationToken>())
            .Returns("Test page");
        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body>Test content</body></html>");
        browser.GetInteractiveElementsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InteractiveElement>>(
                [new InteractiveElement(1, "button", "submit", "Submit", null, null, "#submit")]));
        browser.HasMoreContentAsync(Arg.Any<CancellationToken>())
            .Returns(false);
        browser.GetLastNavigationStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<int?>(200));
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return browser;
    }
}

#pragma warning restore CS0618
