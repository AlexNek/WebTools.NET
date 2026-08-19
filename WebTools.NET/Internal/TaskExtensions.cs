namespace WebTools.NET.Internal;

internal static class TaskExtensions
{
    internal static async Task AwaitWithCancellationAsync(
        this Task operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ObserveCompletionAsync(operation).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    internal static async Task<T> AwaitWithCancellationAsync<T>(
        this Task<T> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ObserveCompletionAsync(operation).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    internal static async Task<T> AwaitWithCancellationAsync<T>(
        this Task<T> operation,
        CancellationToken cancellationToken,
        Func<T, Task> disposeOnCancellation)
    {
        ArgumentNullException.ThrowIfNull(disposeOnCancellation);

        try
        {
            return await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            T result;
            try
            {
                result = await operation.ConfigureAwait(false);
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            try
            {
                await disposeOnCancellation(result).ConfigureAwait(false);
            }
            catch
            {
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static async Task ObserveCompletionAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // The caller's cancellation or original failure is reported by the caller.
        }
    }
}
