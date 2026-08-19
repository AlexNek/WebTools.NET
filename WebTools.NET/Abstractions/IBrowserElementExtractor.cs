using WebTools.NET.Models;

namespace WebTools.NET.Abstractions;

/// <summary>
/// Extracts actionable elements from the current browser page.
/// </summary>
public interface IBrowserElementExtractor
{
    Task<IReadOnlyList<InteractiveElement>> GetInteractiveElementsAsync(CancellationToken ct = default);
}
