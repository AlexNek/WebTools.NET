namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// The deterministic result of analyzing an HTML document.
/// </summary>
public sealed record HtmlAnalysisResult(
    IReadOnlyList<HtmlTextBlock> TextBlocks,
    IReadOnlyList<HtmlStructuredDataRecord> StructuredData,
    IReadOnlyList<HtmlCandidateRegion> CandidateRegions,
    HtmlAnalysisMetadata Metadata);
