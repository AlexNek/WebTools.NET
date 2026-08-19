namespace WebTools.NET.Abstractions;

/// <summary>
/// Captures the current browser page as an image.
/// </summary>
public interface IBrowserScreenshot
{
    Task<string> ScreenshotAsync(CancellationToken ct = default);
}
