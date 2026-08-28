using WebTools.NET.Abstractions;
using WebTools.NET.ContentAnalysis.Abstractions;
using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Composes browser-session HTML retrieval with the independent HTML analyzer.
/// </summary>
public static class BrowserContentAnalysisExtensions
{
    /// <summary>
    /// Analyzes the complete HTML document of the current browser page.
    /// </summary>
    public static async Task<HtmlAnalysisResult> AnalyzeCurrentPageAsync(
        this IBrowserContent browser,
        IHtmlContentAnalyzer analyzer,
        HtmlAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(analyzer);

        var html = await browser.GetHtmlAsync(ct).ConfigureAwait(false);
        var currentUrl = await browser.GetCurrentUrlAsync(ct).ConfigureAwait(false);
        var sourceUri = Uri.TryCreate(currentUrl, UriKind.Absolute, out var parsedUri)
            ? parsedUri
            : null;

        return analyzer.Analyze(html, options, sourceUri);
    }
}
