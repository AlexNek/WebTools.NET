namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// Describes how a structured-data payload was handled.
/// </summary>
public enum HtmlStructuredDataParseStatus
{
    NotApplicable = 0,
    Parsed = 1,
    Empty = 2,
    Malformed = 3,
    Unsupported = 4
}
