namespace WebTools.NET.Models;

/// <summary>
/// Controls how fetched page content is processed before returning.
/// </summary>
public enum EContentFormat
{
    /// <summary>Strip all HTML, collapse whitespace — plain text output.</summary>
    PlainText = 0,

    /// <summary>Convert HTML to GitHub-flavored Markdown preserving structure.</summary>
    Markdown = 1,

    /// <summary>Return rendered body HTML with noise tags removed.</summary>
    Html = 2
}
