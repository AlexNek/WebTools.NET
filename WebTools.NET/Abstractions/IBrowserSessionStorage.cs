namespace WebTools.NET.Abstractions;

/// <summary>
/// Loads and saves browser storage state.
/// </summary>
public interface IBrowserSessionStorage
{
    Task LoadStorageStateAsync(string path, CancellationToken ct = default);

    Task SaveStorageStateAsync(string path, CancellationToken ct = default);
}
