using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Abstractions;

/// <summary>
/// Analyzes a complete HTML document without requiring a browser or network connection.
/// </summary>
public interface IHtmlContentAnalyzer
{
    /// <summary>
    /// Parses and analyzes the supplied complete HTML document.
    /// </summary>
    /// <param name="html">The complete HTML document to analyze.</param>
    /// <param name="options">Optional extraction and output limits.</param>
    /// <param name="sourceUri">Optional source URI used when resolving links.</param>
    /// <returns>A deterministic, bounded analysis result.</returns>
    HtmlAnalysisResult Analyze(
        string html,
        HtmlAnalysisOptions? options = null,
        Uri? sourceUri = null);
}
