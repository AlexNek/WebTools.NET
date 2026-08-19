namespace WebTools.NET.Abstractions;

/// <summary>
/// Provides browser form and control interaction capabilities.
/// </summary>
public interface IBrowserFormInteraction
{
    Task<bool> IsCheckedAsync(string selector, CancellationToken ct = default);

    Task SelectOptionAsync(string selector, string value, CancellationToken ct = default);

    Task SubmitFormAsync(string selector, CancellationToken ct = default);
}
