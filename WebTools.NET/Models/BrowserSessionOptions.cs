namespace WebTools.NET.Models;

/// <summary>
/// Configuration for a stateful browser session and its browser context.
/// </summary>
public sealed class BrowserSessionOptions
{
    private static readonly TimeSpan MaximumSupportedDuration =
        TimeSpan.FromMilliseconds(int.MaxValue);

    private int _maxOperations = 50;

    private TimeSpan _maxDuration = TimeSpan.FromMinutes(5);

    private int _viewportHeight = 1080;

    private int _viewportWidth = 1920;

    /// <summary>Maximum number of operations per session. Default: 50.</summary>
    public int MaxOperations
    {
        get => _maxOperations;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxOperations), value,
                    "MaxOperations must be greater than zero.");
            }

            _maxOperations = value;
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

    /// <summary>Default content format for browser snapshots. Default: Markdown.</summary>
    public EContentFormat DefaultFormat { get; init; } = EContentFormat.Markdown;

    /// <summary>When true, each snapshot includes a base64-encoded PNG screenshot. Default: false.</summary>
    public bool IncludeScreenshot { get; init; }

    /// <summary>
    /// File path for persisting browser storage state (cookies and local storage).
    /// </summary>
    public string? StorageStatePath { get; init; }

    /// <summary>Viewport width in pixels. Default: 1920.</summary>
    public int ViewportWidth
    {
        get => _viewportWidth;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ViewportWidth), value,
                    "ViewportWidth must be greater than zero.");
            }

            _viewportWidth = value;
        }
    }

    /// <summary>Viewport height in pixels. Default: 1080.</summary>
    public int ViewportHeight
    {
        get => _viewportHeight;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ViewportHeight), value,
                    "ViewportHeight must be greater than zero.");
            }

            _viewportHeight = value;
        }
    }
}
