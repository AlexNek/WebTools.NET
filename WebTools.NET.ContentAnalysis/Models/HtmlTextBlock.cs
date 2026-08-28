namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// A meaningful semantic text block extracted from a document.
/// </summary>
public sealed record HtmlTextBlock(
    string Text,
    HtmlTextBlockKind Kind,
    int DocumentOrder,
    HtmlSourceLocation SourceLocation,
    IReadOnlyList<HtmlLink> Links);
