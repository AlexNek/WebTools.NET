using FluentAssertions;

using WebTools.NET.Internal;

using Xunit;

namespace WebTools.NET.Tests;

public class TaskExtensionsTests
{
    [Fact]
    public async Task AwaitWithCancellationAsync_WaitsForUnderlyingOperationBeforeThrowing()
    {
        // Arrange
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var awaited = release.Task.AwaitWithCancellationAsync(cts.Token);

        // Act
        cts.Cancel();
        await Task.Delay(50);

        // Assert
        awaited.IsCompleted.Should().BeFalse();
        release.SetResult();
        Func<Task> act = async () => await awaited;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
