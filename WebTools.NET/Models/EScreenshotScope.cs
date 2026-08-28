namespace WebTools.NET.Models;

/// <summary>
/// Controls how much of the page a screenshot captures.
/// </summary>
public enum EScreenshotScope
{
    /// <summary>Capture the visible viewport only.</summary>
    Viewport = 0,

    /// <summary>Capture the full scrollable page.</summary>
    FullPage = 1
}
