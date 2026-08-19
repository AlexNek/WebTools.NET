namespace WebTools.NET.Abstractions;

/// <summary>
/// Reads the most recently observed document navigation status.
/// </summary>
public interface IBrowserNavigationStatus
{
    Task<int?> GetLastNavigationStatusAsync(CancellationToken ct = default);
}
