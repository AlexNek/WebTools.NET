using FluentAssertions;

using NSubstitute;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.ContentAnalysis.Analysis;

using Xunit;

namespace WebTools.NET.Tests;

public class BrowserContentAnalysisExtensionsTests
{
    [Fact]
    public async Task AnalyzeCurrentPageAsync_AnalyzesCompleteCurrentDocument()
    {
        // Arrange
        var browser = Substitute.For<IBrowserContent>();
        browser.GetHtmlAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html><head><meta property='og:title' content='Test'></head>" +
                                     "<body><h1>Pricing</h1><p>$9 per month</p></body></html>"));
        browser.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://test.example.com/pricing"));
        var analyzer = new HtmlContentAnalyzer();

        // Act
        var result = await browser.AnalyzeCurrentPageAsync(analyzer);

        // Assert
        result.TextBlocks.Should().Contain(block => block.Text == "Pricing");
        result.StructuredData.Should().Contain(record => record.SourceType == "Metadata");
        result.CandidateRegions.Should().Contain(candidate => candidate.Signals.Contains("currency"));
        await browser.Received(1).GetHtmlAsync(Arg.Any<CancellationToken>());
        await browser.Received(1).GetCurrentUrlAsync(Arg.Any<CancellationToken>());
    }
}
