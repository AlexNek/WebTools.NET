namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// Controls HTML normalization, extraction, and result bounds.
/// </summary>
public sealed record HtmlAnalysisOptions
{
    public int MaxInputLength { get; init; } = 1_000_000;

    public int MaxTextBlocks { get; init; } = 200;

    public int MaxStructuredDataRecords { get; init; } = 50;

    public int MaxStructuredDataPayloadLength { get; init; } = 100_000;

    public int MaxCandidateRegions { get; init; } = 20;

    public int MaxCandidateFragmentLength { get; init; } = 2_000;

    public int NearbyTextWindow { get; init; } = 300;

    /// <summary>
    /// Text terms that should produce the <c>keyword</c> candidate signal.
    /// No topic-specific keywords are enabled by default.
    /// </summary>
    public IReadOnlyList<string> CandidateKeywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Class/id terms that should produce the <c>structure</c> candidate signal.
    /// No topic-specific structural terms are enabled by default.
    /// </summary>
    public IReadOnlyList<string> StructuralTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Weights for recognized candidate signals. A missing or zero-weight signal is disabled.
    /// </summary>
    public IReadOnlyDictionary<string, double> SignalWeights { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = 3d,
            ["currency"] = 4d,
            ["amount"] = 1d,
            ["period"] = 2d,
            ["structure"] = 2d,
            ["heading"] = 2d,
            ["repeated"] = 1d
        };

    public bool RemoveScripts { get; init; } = true;

    public bool RemoveStyles { get; init; } = true;

    public bool RemoveComments { get; init; } = true;

    public bool RemoveHiddenElements { get; init; } = true;

    public bool RemoveNavigationRegions { get; init; } = true;

    public bool RemoveTrackingElements { get; init; } = true;

    public bool RemoveDecorativeSvg { get; init; } = true;
}
