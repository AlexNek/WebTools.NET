namespace WebTools.NET.Models;

/// <summary>
/// Legacy browser-agent options retained for source compatibility.
/// Use <see cref="BrowserSessionOptions"/> for new code.
/// </summary>
[Obsolete("Use BrowserSessionOptions instead.")]
public sealed class BrowserAgentOptions
{
    private static readonly TimeSpan MaximumSupportedDuration =
        TimeSpan.FromMilliseconds(int.MaxValue);

    private int _maxActions = 50;

    private TimeSpan _maxDuration = TimeSpan.FromMinutes(5);

    /// <summary>Maximum number of actions per session. Default: 50.</summary>
    public int MaxActions
    {
        get => _maxActions;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxActions), value,
                    "MaxActions must be greater than zero.");
            }

            _maxActions = value;
        }
    }

    /// <summary>Maximum session duration. Default: 5 minutes.</summary>
    public TimeSpan MaxDuration
    {
        get => _maxDuration;
        init
        {
            if (value <= TimeSpan.Zero || value > MaximumSupportedDuration)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxDuration), value,
                    $"MaxDuration must be greater than zero and no more than {MaximumSupportedDuration}.");
            }

            _maxDuration = value;
        }
    }

    /// <summary>Default content format for page snapshots. Default: Markdown.</summary>
    public EContentFormat DefaultFormat { get; init; } = EContentFormat.Markdown;

    /// <summary>When true, each snapshot includes a base64-encoded PNG screenshot. Default: false.</summary>
    public bool IncludeScreenshot { get; init; }

    /// <summary>File path for persisting browser storage state.</summary>
    public string? StorageStatePath { get; init; }

    /// <summary>
    /// Legacy nested browser-session options. It remains for source compatibility;
    /// an explicitly supplied browser session is still caller-owned.
    /// </summary>
    public BrowserSessionOptions? SessionOptions { get; init; }
}
