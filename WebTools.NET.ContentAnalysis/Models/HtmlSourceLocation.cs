namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// Identifies an element's deterministic position in the source document.
/// </summary>
public sealed record HtmlSourceLocation(
    string Selector,
    string ElementName,
    int DocumentOrder);
