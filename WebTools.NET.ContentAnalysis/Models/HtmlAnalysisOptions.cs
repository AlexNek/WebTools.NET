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

    public bool RemoveScripts { get; init; } = true;

    public bool RemoveStyles { get; init; } = true;

    public bool RemoveComments { get; init; } = true;

    public bool RemoveHiddenElements { get; init; } = true;

    public bool RemoveNavigationRegions { get; init; } = true;

    public bool RemoveTrackingElements { get; init; } = true;

    public bool RemoveDecorativeSvg { get; init; } = true;
}
