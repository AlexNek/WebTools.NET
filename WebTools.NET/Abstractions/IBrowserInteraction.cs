namespace WebTools.NET.Abstractions;

/// <summary>
/// Minimal browser interface for navigation and direct page interaction.
/// Additional browser-session capabilities are exposed by <see cref="IBrowserSession"/>.
/// </summary>
public interface IBrowserInteraction : IBrowserContent
{
    Task ClickAsync(string selector, CancellationToken ct = default);

    Task FillAsync(string selector, string value, CancellationToken ct = default);
}
