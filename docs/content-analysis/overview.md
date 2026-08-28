# HTML Content Analysis

`WebTools.NET.ContentAnalysis` analyzes complete HTML documents without a browser or network connection. It extracts meaningful text, inspects embedded structured data, detects relevant signals, and returns bounded ranked candidate regions.

## Installation

The analyzer is available through the `WebTools.NET` package. It can also be consumed directly from the companion package:

```bash
dotnet add package WebTools.NET.ContentAnalysis
```

## Static HTML

Resolve `IHtmlContentAnalyzer` from dependency injection or construct `HtmlContentAnalyzer` directly:

```csharp
var result = analyzer.Analyze(html, sourceUri: new Uri("https://test.example.com/pricing"));

foreach (var candidate in result.CandidateRegions)
{
    Console.WriteLine($"{candidate.Rank}: {candidate.Text}");
}
```

The analyzer accepts the complete document rather than only `body` HTML. This preserves head metadata, JSON-LD, and embedded application state. Structured-data errors are isolated and do not stop normal text extraction.

## Browser current page

The existing browser interfaces remain unchanged. Retrieve the complete current document, then analyze it:

```csharp
var html = await browser.GetHtmlAsync();
var result = analyzer.Analyze(html, sourceUri: new Uri(await browser.GetCurrentUrlAsync()));
```

`IWebContentFetcher` is intentionally not extended because its formatted results are body-oriented. Use `IBrowserContent.GetHtmlAsync()` when head metadata and structured data are required.

## Results and limits

`HtmlAnalysisResult` contains:

- semantic `TextBlocks` with source locations and links;
- `StructuredData` records for JSON-LD, application state, metadata, and useful `data-*` values;
- ranked, bounded `CandidateRegions` with signals, scores, context, and source locations;
- `Metadata` describing input/result truncation and diagnostics.

Use `HtmlAnalysisOptions` to limit input size, structured-data payloads, text blocks, candidates, fragment length, and nearby context. Ranking is deterministic: higher scores rank first, followed by document order.
