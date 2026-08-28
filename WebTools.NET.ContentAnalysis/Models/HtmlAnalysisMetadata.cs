namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// Describes input and output bounds applied during analysis.
/// </summary>
public sealed record HtmlAnalysisMetadata(
    int OriginalInputLength,
    int AnalyzedInputLength,
    bool InputTruncated,
    bool ResultsTruncated,
    int OmittedTextBlocks,
    int OmittedStructuredDataRecords,
    int OmittedCandidateRegions,
    IReadOnlyList<string> Diagnostics);
