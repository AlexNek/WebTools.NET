using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed record HtmlCandidateMatch(
    HtmlTextBlock Block,
    IReadOnlyList<string> Signals,
    double Score);
