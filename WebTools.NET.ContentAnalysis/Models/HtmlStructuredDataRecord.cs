using System.Text.Json;

namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// A structured-data or metadata record found in the document.
/// </summary>
public sealed record HtmlStructuredDataRecord(
    string SourceType,
    HtmlSourceLocation SourceLocation,
    string RawPayload,
    HtmlStructuredDataParseStatus ParseStatus,
    JsonElement? ParsedValue,
    bool IsTruncated);
