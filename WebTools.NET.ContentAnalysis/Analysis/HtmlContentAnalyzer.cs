using AngleSharp.Html.Parser;

using WebTools.NET.ContentAnalysis.Abstractions;
using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

/// <summary>
/// Provides deterministic, browser-independent HTML content analysis.
/// </summary>
public sealed class HtmlContentAnalyzer : IHtmlContentAnalyzer
{
    private readonly HtmlDocumentNormalizer _normalizer;
    private readonly HtmlStructuredDataExtractor _structuredDataExtractor;
    private readonly HtmlTextBlockExtractor _textBlockExtractor;
    private readonly HtmlCandidateDetector _candidateDetector;
    private readonly HtmlCandidateRegionBuilder _candidateRegionBuilder;

    public HtmlContentAnalyzer()
    {
        _normalizer = new HtmlDocumentNormalizer();
        _structuredDataExtractor = new HtmlStructuredDataExtractor();
        _textBlockExtractor = new HtmlTextBlockExtractor();
        _candidateDetector = new HtmlCandidateDetector();
        _candidateRegionBuilder = new HtmlCandidateRegionBuilder();
    }

    public HtmlAnalysisResult Analyze(
        string html,
        HtmlAnalysisOptions? options = null,
        Uri? sourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        var effectiveOptions = options ?? new HtmlAnalysisOptions();
        HtmlAnalysisOptionsValidator.Validate(effectiveOptions);

        var analyzedHtml = LimitInput(html, effectiveOptions.MaxInputLength, out var inputTruncated);
        var diagnostics = CreateDiagnostics(inputTruncated);

        var document = new HtmlParser().ParseDocument(analyzedHtml);
        var locations = new HtmlSourceLocationMap(document);
        var structuredData = _structuredDataExtractor.Extract(
            document,
            locations,
            effectiveOptions,
            diagnostics,
            out var omittedStructuredDataRecords);

        _normalizer.Normalize(document, effectiveOptions);
        var blocks = _textBlockExtractor.Extract(document, locations, sourceUri);
        var visibleBlocks = blocks
            .Take(effectiveOptions.MaxTextBlocks)
            .ToList();
        var omittedTextBlocks = Math.Max(0, blocks.Count - visibleBlocks.Count);

        var candidateMatches = _candidateDetector.Detect(blocks, effectiveOptions);
        var omittedCandidateRegions = Math.Max(0, candidateMatches.Count - effectiveOptions.MaxCandidateRegions);
        var candidates = candidateMatches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Block.DocumentOrder)
            .Take(effectiveOptions.MaxCandidateRegions)
            .Select((match, index) => _candidateRegionBuilder.Build(match, blocks, index + 1, effectiveOptions))
            .ToList();

        var metadata = CreateMetadata(
            html.Length,
            analyzedHtml.Length,
            inputTruncated,
            omittedTextBlocks,
            omittedStructuredDataRecords,
            structuredData,
            omittedCandidateRegions,
            candidates,
            diagnostics);

        return new HtmlAnalysisResult(
            visibleBlocks.Select(entry => entry.Block).ToList().AsReadOnly(),
            structuredData.AsReadOnly(),
            candidates.AsReadOnly(),
            metadata);
    }

    private static List<string> CreateDiagnostics(bool inputTruncated)
    {
        var diagnostics = new List<string>();
        if (inputTruncated)
        {
            diagnostics.Add("Input HTML was truncated to the configured maximum length.");
        }

        return diagnostics;
    }

    private static HtmlAnalysisMetadata CreateMetadata(
        int originalInputLength,
        int analyzedInputLength,
        bool inputTruncated,
        int omittedTextBlocks,
        int omittedStructuredDataRecords,
        IReadOnlyList<HtmlStructuredDataRecord> structuredData,
        int omittedCandidateRegions,
        IReadOnlyList<HtmlCandidateRegion> candidates,
        IReadOnlyCollection<string> diagnostics)
    {
        var resultsTruncated = omittedTextBlocks > 0 ||
                               omittedStructuredDataRecords > 0 ||
                               structuredData.Any(record => record.IsTruncated) ||
                               omittedCandidateRegions > 0 ||
                               candidates.Any(candidate => candidate.IsTruncated);

        return new HtmlAnalysisMetadata(
            originalInputLength,
            analyzedInputLength,
            inputTruncated,
            resultsTruncated,
            omittedTextBlocks,
            omittedStructuredDataRecords,
            omittedCandidateRegions,
            diagnostics.ToList().AsReadOnly());
    }

    private static string LimitInput(string html, int maxLength, out bool truncated)
    {
        truncated = html.Length > maxLength;
        return truncated ? html[..maxLength] : html;
    }
}
