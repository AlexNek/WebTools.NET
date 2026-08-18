namespace WebTools.NET.Models;

/// <summary>
/// Controls how aggressively noise tags are removed from HTML before conversion.
/// </summary>
public enum ESanitizeLevel
{
    /// <summary>Remove script, style, nav, footer, and header. Best for reading article content.</summary>
    Strict = 0,

    /// <summary>Remove only script and style. Keeps nav, footer, header for page discovery.</summary>
    Minimal = 1,

    /// <summary>No sanitization — return body HTML as-is.</summary>
    None = 2
}
