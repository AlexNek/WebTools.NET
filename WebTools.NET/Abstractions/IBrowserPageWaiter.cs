namespace WebTools.NET.Abstractions;

/// <summary>
/// Waits for page conditions.
/// </summary>
public interface IBrowserPageWaiter
{
    Task WaitForSelectorAsync(string selector, int timeoutMs, CancellationToken ct = default);
}
