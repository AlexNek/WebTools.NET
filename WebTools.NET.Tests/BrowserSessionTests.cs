using FluentAssertions;

using NSubstitute;

using WebTools.NET.Abstractions;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserSessionTests
{
    [Fact]
    public async Task StartAsync_WhenNavigationReturnsHttpError_SetsSnapshotError()
    {
        // Arrange
        var browser = CreateBrowser(statusCode: 404);
        await using var sut = new BrowserSession(browser);

        // Act
        var snapshot = await sut.StartAsync("https://test.example.com/missing");

        // Assert
        snapshot.StatusCode.Should().Be(404);
        snapshot.Error.Should().Be("HTTP 404");
    }

    [Fact]
    public async Task ExecuteAsync_ClickResolvesElementAndReturnsUpdatedSnapshot()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.Click,
            ElementIndex: 1));

        // Assert
        snapshot.Error.Should().BeNull();
        await browser.Received().ClickAsync("#submit", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionFails_ReturnsErrorAndKeepsSessionAlive()
    {
        // Arrange
        var browser = CreateBrowser();
        browser.ClickAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Element is stale.")));
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com");

        // Act
        var failed = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.Click,
            ElementIndex: 1));
        var recovered = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        failed.Error.Should().Be("Element is stale.");
        recovered.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesMaximumActionsPerSession()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxOperations = 1 });
        await sut.StartAsync("https://test.example.com");

        // Act
        var first = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));
        var second = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        first.Error.Should().BeNull();
        second.Error.Should().Be("Operation limit (1) reached.");
    }

    [Fact]
    public async Task StartAsync_RestartClearsPreviousSessionHistory()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com/first");
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Act
        await sut.StartAsync("https://test.example.com/second");

        // Assert
        sut.OperationHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_LoadsStorageStateBeforeNavigation_AndDisposeSavesIt()
    {
        // Arrange
        var browser = CreateBrowser();
        var options = new BrowserSessionOptions { StorageStatePath = "fake-storage-state.json" };
        var sut = new BrowserSession(browser, options);

        // Act
        await sut.StartAsync("https://test.example.com");
        await sut.DisposeAsync();

        // Assert
        await browser.Received().LoadStorageStateAsync(options.StorageStatePath, Arg.Any<CancellationToken>());
        await browser.Received().SaveStorageStateAsync(options.StorageStatePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_PropagatesCancellation()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(browser);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => sut.StartAsync("https://test.example.com", ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_FillFormDispatchesTextAndCheckboxFields()
    {
        // Arrange
        var browser = CreateBrowser(
            elements:
            [
                new InteractiveElement(1, "input", "text", "Name", null, "name", "#name"),
                new InteractiveElement(2, "input", "checkbox", "Agree", null, "agree", "#agree")
            ]);
        browser.IsCheckedAsync("#agree", Arg.Any<CancellationToken>()).Returns(false);
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com/form");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.FillForm,
            Fields:
            [
                new FormFieldValue(1, "Ada"),
                new FormFieldValue(2, "true")
            ]));

        // Assert
        snapshot.Error.Should().BeNull();
        await browser.Received().FillAsync("#name", "Ada", Arg.Any<CancellationToken>());
        await browser.Received().ClickAsync("#agree", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenFormatIsOmitted_UsesConfiguredDefaultFormat()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { DefaultFormat = EContentFormat.Html });

        // Act
        var snapshot = await sut.StartAsync("https://test.example.com");

        // Assert
        snapshot.Format.Should().Be(EContentFormat.Html);
    }

    [Fact]
    public async Task ExecuteAsync_FillFormWithInvalidCheckboxValue_ReturnsValidationError()
    {
        // Arrange
        var browser = CreateBrowser(
            elements:
            [new InteractiveElement(1, "input", "checkbox", "Agree", null, "agree", "#agree")]);
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com/form");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.FillForm,
            Fields: [new FormFieldValue(1, "yes")]));

        // Assert
        snapshot.Error.Should().Contain("true").And.Contain("false");
        await browser.DidNotReceive().ClickAsync("#agree", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDisposeExplicitlySuppliedSession()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com");

        // Act
        await sut.DisposeAsync();

        // Assert
        await browser.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_PreventsFurtherOperations()
    {
        // Arrange
        var browser = CreateBrowser();
        var sut = new BrowserSession(browser);

        // Act
        await sut.DisposeAsync();
        var act = () => sut.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task StartAsync_WhenStorageStateLoadFails_LeavesSessionNotStarted()
    {
        // Arrange
        var browser = CreateBrowser();
        browser.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Storage state is invalid.")));
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { StorageStatePath = "fake-storage-state.json" });

        // Act
        var failedStart = await sut.StartAsync("https://test.example.com");
        var afterFailure = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        failedStart.Error.Should().Be("Storage state is invalid.");
        afterFailure.Error.Should().Be("Session not started. Call StartAsync first.");
        await browser.DidNotReceive().NavigateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenSessionDeadlineExpiresDuringNavigation_LeavesSessionNotStarted()
    {
        // Arrange
        var browser = CreateBrowser();
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(1000, call.Arg<CancellationToken>()));
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(50) });

        // Act
        var snapshot = await sut.StartAsync("https://test.example.com");

        // Assert
        snapshot.Error.Should().Contain("Session duration limit");
        var afterFailure = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));
        afterFailure.Error.Should().Be("Session not started. Call StartAsync first.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSnapshotAssemblyExceedsDeadline_ReturnsDurationError()
    {
        // Arrange
        var browser = CreateBrowser();
        var htmlCalls = 0;

        async Task<string> DelayedHtmlAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
            return "<html><body>Late content</body></html>";
        }

        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns(call => Interlocked.Increment(ref htmlCalls) == 1
                ? Task.FromResult("<html><body>Test content</body></html>")
                : DelayedHtmlAsync(call.Arg<CancellationToken>()));
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(200) });
        await sut.StartAsync("https://test.example.com");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        snapshot.Error.Should().Contain("Session duration limit");
        snapshot.Url.Should().Be("https://test.example.com/");
    }

    [Fact]
    public async Task StartAsync_WhenRestartIsCancelled_LeavesAgentNotStarted()
    {
        // Arrange
        var browser = CreateBrowser();
        using var cts = new CancellationTokenSource();
        var navigationCalls = 0;
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (Interlocked.Increment(ref navigationCalls) == 2)
                {
                    cts.Cancel();
                    return Task.FromCanceled(cts.Token);
                }

                return Task.CompletedTask;
            });
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com/first");
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Act
        var act = () => sut.StartAsync("https://test.example.com/second", ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        var afterCancellation = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));
        afterCancellation.Error.Should().Be("Session not started. Call StartAsync first.");
    }

    [Fact]
    public async Task StartAsync_WhenNavigationFailsBeforePageReady_LeavesSessionNotStarted()
    {
        // Arrange
        var browser = Substitute.For<IBrowserSession, IBrowserSessionState>();
        var state = (IBrowserSessionState)browser;
        state.IsPageReady.Returns(false);
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Browser context failed.")));
        await using var sut = new BrowserSession(browser);

        // Act
        var failedStart = await sut.StartAsync("https://test.example.com");
        var afterFailure = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        failedStart.Error.Should().Be("Browser context failed.");
        afterFailure.Error.Should().Be("Session not started. Call StartAsync first.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenBuiltInLifecycleOperationIgnoresCancellation_ResetsAndReturns()
    {
        // Arrange
        var browser = Substitute.For<IBrowserSession, IBrowserSessionLifecycle>();
        var lifecycle = (IBrowserSessionLifecycle)browser;
        browser.GetCurrentUrlAsync(Arg.Any<CancellationToken>()).Returns("https://test.example.com/");
        browser.GetTitleAsync(Arg.Any<CancellationToken>()).Returns("Test page");
        browser.GetHtmlAsync(Arg.Any<CancellationToken>()).Returns("<html><body>Test content</body></html>");
        browser.GetInteractiveElementsAsync(Arg.Any<CancellationToken>())
            .Returns([new InteractiveElement(1, "button", "button", "Submit", null, null, "#submit")]);
        browser.HasMoreContentAsync(Arg.Any<CancellationToken>()).Returns(false);
        browser.GetLastNavigationStatusAsync(Arg.Any<CancellationToken>()).Returns(200);
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var releaseAction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.ClickAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(releaseAction.Task);
        lifecycle.ResetAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            releaseAction.TrySetResult();
            return Task.CompletedTask;
        });

        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(50) });
        await sut.StartAsync("https://test.example.com");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.Click,
            ElementIndex: 1));

        // Assert
        snapshot.Error.Should().Contain("Session duration limit");
        await lifecycle.Received().ResetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSnapshotAsync_AfterDurationExpires_UsesLastSnapshotWithoutBrowserWork()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(50) });
        await sut.StartAsync("https://test.example.com");
        browser.ClearReceivedCalls();
        await Task.Delay(100);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        snapshot.Error.Should().Contain("Session duration limit");
        snapshot.Url.Should().Be("https://test.example.com/");
        _ = browser.DidNotReceive().GetCurrentUrlAsync(Arg.Any<CancellationToken>());
        _ = browser.DidNotReceive().GetHtmlAsync(Arg.Any<CancellationToken>());
    }

    private static IBrowserSession CreateBrowser(
        int? statusCode = 200,
        IReadOnlyList<InteractiveElement>? elements = null)
    {
        var browser = Substitute.For<IBrowserSession>();
        browser.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns("https://test.example.com/");
        browser.GetTitleAsync(Arg.Any<CancellationToken>())
            .Returns("Test page");
        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns("<html><body>Test content</body></html>");
        browser.GetInteractiveElementsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InteractiveElement>>(
                elements ??
                [new InteractiveElement(1, "button", "submit", "Submit", null, null, "#submit")]));
        browser.HasMoreContentAsync(Arg.Any<CancellationToken>())
            .Returns(false);
        browser.GetLastNavigationStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(statusCode));
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return browser;
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionLimitReached_PreservesCurrentSnapshotState()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxOperations = 1 });
        await sut.StartAsync("https://test.example.com");
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        snapshot.Error.Should().Be("Operation limit (1) reached.");
        snapshot.Url.Should().Be("https://test.example.com/");
        snapshot.StatusCode.Should().Be(200);
        snapshot.Elements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FillFormWithInvalidLaterField_DoesNotMutateEarlierFields()
    {
        // Arrange
        var browser = CreateBrowser(
            elements:
            [
                new InteractiveElement(1, "input", "text", "Name", null, "name", "#name"),
                new InteractiveElement(2, "input", "checkbox", "Agree", null, "agree", "#agree")
            ]);
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com/form");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.FillForm,
            Fields:
            [
                new FormFieldValue(1, "Ada"),
                new FormFieldValue(2, "yes")
            ]));

        // Assert
        snapshot.Error.Should().Contain("true").And.Contain("false");
        await browser.DidNotReceive().FillAsync("#name", "Ada", Arg.Any<CancellationToken>());
        await browser.DidNotReceive().ClickAsync("#agree", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ScrollDown_UsesBrowserViewportHeight()
    {
        // Arrange
        var browser = CreateBrowser();
        browser.GetViewportHeightAsync(Arg.Any<CancellationToken>()).Returns(720);
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com");

        // Act
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.ScrollDown));

        // Assert
        await browser.Received().ScrollAsync(720, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenNavigationIsCancelled_LeavesSessionNotStarted()
    {
        // Arrange
        var browser = CreateBrowser();
        using var cts = new CancellationTokenSource();
        browser.NavigateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            });
        await using var sut = new BrowserSession(browser);

        // Act
        var act = () => sut.StartAsync("https://test.example.com", ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));
        snapshot.Error.Should().Be("Session not started. Call StartAsync first.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDurationExpires_ReturnsCurrentSnapshotState()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(1) });
        await sut.StartAsync("https://test.example.com");
        await Task.Delay(20);

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Snapshot));

        // Assert
        snapshot.Error.Should().Contain("Session duration limit");
        snapshot.Url.Should().Be("https://test.example.com/");
        var current = await sut.GetSnapshotAsync();
        current.Error.Should().Contain("Session duration limit");
        current.Url.Should().Be("https://test.example.com/");
    }

    [Fact]
    public async Task StartAsync_WhenScreenshotFails_PreservesSuccessfulPageSnapshot()
    {
        // Arrange
        var browser = CreateBrowser();
        browser.ScreenshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("Screenshot unavailable.")));
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { IncludeScreenshot = true });

        // Act
        var snapshot = await sut.StartAsync("https://test.example.com");

        // Assert
        snapshot.Error.Should().BeNull();
        snapshot.StatusCode.Should().Be(200);
        snapshot.ScreenshotBase64.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_WhenElementExtractionFails_PreservesNavigationStatus()
    {
        // Arrange
        var browser = CreateBrowser(statusCode: 404);
        browser.GetInteractiveElementsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<InteractiveElement>>(
                new InvalidOperationException("Extraction failed.")));
        await using var sut = new BrowserSession(browser);

        // Act
        var snapshot = await sut.StartAsync("https://test.example.com/missing");

        // Assert
        snapshot.StatusCode.Should().Be(404);
        snapshot.Error.Should().Be("HTTP 404");
    }

    [Fact]
    public async Task ExecuteAsync_DispatchesTheRemainingActionTypes()
    {
        // Arrange
        var browser = CreateBrowser();
        await using var sut = new BrowserSession(browser);
        await sut.StartAsync("https://test.example.com");

        // Act
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Navigate, Value: "https://test.example.com/next"));
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Fill, ElementIndex: 1, Value: "value"));
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Select, ElementIndex: 1, Value: "Option"));
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Submit, ElementIndex: 1));
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.WaitFor, Value: "#results", TimeoutMs: 1000));
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.Back));
        await sut.ExecuteAsync(new BrowserOperation(EBrowserOperationType.ScrollUp));

        // Assert
        await browser.Received().NavigateAsync("https://test.example.com/next", Arg.Any<CancellationToken>());
        await browser.Received().FillAsync("#submit", "value", Arg.Any<CancellationToken>());
        await browser.Received().SelectOptionAsync("#submit", "Option", Arg.Any<CancellationToken>());
        await browser.Received().SubmitFormAsync("#submit", Arg.Any<CancellationToken>());
        await browser.Received().WaitForSelectorAsync("#results", 1000, Arg.Any<CancellationToken>());
        await browser.Received().GoBackAsync(Arg.Any<CancellationToken>());
        await browser.Received().ScrollAsync(-1080, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDurationExpires_UsesNonCancelledTokenForRecoverySnapshot()
    {
        // Arrange
        var browser = CreateBrowser();
        browser.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();
                return token.IsCancellationRequested
                    ? Task.FromCanceled<string>(token)
                    : Task.FromResult("https://test.example.com/");
            });
        browser.ClickAsync("#submit", Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(1000, call.Arg<CancellationToken>()));
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(50) });
        await sut.StartAsync("https://test.example.com");

        // Act
        var snapshot = await sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.Click,
            ElementIndex: 1));

        // Assert
        snapshot.Error.Should().Contain("Session duration limit");
        snapshot.Url.Should().Be("https://test.example.com/");
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionIgnoresCancellation_WaitsBeforeReleasingOperation()
    {
        // Arrange
        var browser = CreateBrowser();
        var actionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.ClickAsync("#submit", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                actionStarted.SetResult();
                await releaseAction.Task;
            });
        await using var sut = new BrowserSession(
            browser,
            new BrowserSessionOptions { MaxDuration = TimeSpan.FromMilliseconds(50) });
        await sut.StartAsync("https://test.example.com");

        // Act
        var execution = sut.ExecuteAsync(new BrowserOperation(
            EBrowserOperationType.Click,
            ElementIndex: 1));
        await actionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(100);

        // Assert
        execution.IsCompleted.Should().BeFalse();
        releaseAction.SetResult();
        var snapshot = await execution;
        snapshot.Error.Should().Contain("Session duration limit");
    }
}
