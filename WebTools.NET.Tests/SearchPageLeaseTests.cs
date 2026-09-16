using FluentAssertions;
using NSubstitute;

using Microsoft.Playwright;

using WebTools.NET.Search;

using Xunit;

namespace WebTools.NET.Tests;

public class SearchPageLeaseTests
{
    [Fact]
    public async Task DisposeAsync_DisposesPageResourcesAndReleaseExactlyOnce()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        var resourceDisposals = 0;
        var releases = 0;
        var sut = new SearchPageLease(
            page,
            () =>
            {
                resourceDisposals++;
                return ValueTask.CompletedTask;
            },
            () =>
            {
                releases++;
                return ValueTask.CompletedTask;
            });

        // Act
        await sut.DisposeAsync();
        await sut.DisposeAsync();

        // Assert
        await page.Received(1).DisposeAsync();
        resourceDisposals.Should().Be(1);
        releases.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenPageCleanupFails_StillDisposesResourcesAndReleasesOwnership()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.DisposeAsync()
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("page cleanup failed"))));
        var resourceDisposals = 0;
        var releases = 0;
        var sut = new SearchPageLease(
            page,
            () =>
            {
                resourceDisposals++;
                return ValueTask.CompletedTask;
            },
            () =>
            {
                releases++;
                return ValueTask.CompletedTask;
            });

        // Act
        var act = () => sut.DisposeAsync().AsTask();
        await act.Should().NotThrowAsync();

        // Assert
        resourceDisposals.Should().Be(1);
        releases.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenResourceCleanupFails_StillReleasesOwnership()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        var releases = 0;
        var sut = new SearchPageLease(
            page,
            () => new ValueTask(Task.FromException(new InvalidOperationException("resource cleanup failed"))),
            () =>
            {
                releases++;
                return ValueTask.CompletedTask;
            });

        // Act
        var act = () => sut.DisposeAsync().AsTask();
        await act.Should().NotThrowAsync();

        // Assert
        releases.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_ReleasesFallbackOwnershipForTheNextLease()
    {
        // Arrange
        using var ownership = new SemaphoreSlim(1, 1);
        await ownership.WaitAsync();
        var first = new SearchPageLease(
            Substitute.For<IPage>(),
            () => ValueTask.CompletedTask,
            () =>
            {
                ownership.Release();
                return ValueTask.CompletedTask;
            });
        var secondAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Task.Run(async () =>
        {
            await ownership.WaitAsync();
            try
            {
                secondAcquired.SetResult(true);
            }
            finally
            {
                ownership.Release();
            }
        });

        // Act
        await Task.Delay(25);
        secondAcquired.Task.IsCompleted.Should().BeFalse();
        await first.DisposeAsync();
        await second;

        // Assert
        (await secondAcquired.Task).Should().BeTrue();
    }
}
