namespace WebTools.NET.Models;

/// <summary>
/// Configuration options for the <see cref="WebTools.NET.BrowserAgent"/>.
/// </summary>
public sealed class BrowserAgentOptions
{
    /// <summary>Maximum number of actions per session before the agent refuses further commands. Default: 50.</summary>
    public int MaxActions { get; init; } = 50;

    /// <summary>Maximum session duration. Default: 5 minutes.</summary>
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Default content format for page snapshots. Default: Markdown.</summary>
    public EContentFormat DefaultFormat { get; init; } = EContentFormat.Markdown;

    /// <summary>When true, each snapshot includes a base64-encoded PNG screenshot. Default: false.</summary>
    public bool IncludeScreenshot { get; init; }

    /// <summary>
    /// File path for persisting browser storage state (cookies, localStorage).
    /// When set, cookies are loaded at session start and saved after navigation.
    /// Null means no persistence.
    /// </summary>
    public string? StorageStatePath { get; init; }
}
