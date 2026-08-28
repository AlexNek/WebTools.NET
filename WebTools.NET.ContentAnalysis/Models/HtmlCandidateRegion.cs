namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// A bounded region selected as a likely relevant content candidate.
/// </summary>
public sealed record HtmlCandidateRegion(
    string Text,
    string Context,
    IReadOnlyList<string> Signals,
    double Score,
    int Rank,
    HtmlSourceLocation SourceLocation,
    bool IsTruncated);
