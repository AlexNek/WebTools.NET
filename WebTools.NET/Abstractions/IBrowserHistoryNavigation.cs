namespace WebTools.NET.Abstractions;

/// <summary>
/// Provides browser history navigation.
/// </summary>
public interface IBrowserHistoryNavigation
{
    Task GoBackAsync(CancellationToken ct = default);
}
