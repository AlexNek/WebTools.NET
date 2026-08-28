using WebTools.NET.Models;

namespace WebTools.NET.Abstractions;

/// <summary>
/// Captures the current browser page as an image.
/// </summary>
public interface IBrowserScreenshot
{
    Task<string> ScreenshotAsync(
        EScreenshotScope scope = EScreenshotScope.Viewport,
        CancellationToken ct = default);
}
