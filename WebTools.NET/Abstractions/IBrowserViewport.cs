namespace WebTools.NET.Abstractions;

/// <summary>
/// Provides viewport and scrolling capabilities.
/// </summary>
public interface IBrowserViewport
{
    Task<bool> HasMoreContentAsync(CancellationToken ct = default);

    Task<int> GetViewportHeightAsync(CancellationToken ct = default);

    Task ScrollAsync(int deltaY, CancellationToken ct = default);
}
