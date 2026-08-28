using FluentAssertions;

using WebTools.NET.ContentAnalysis.Analysis;
using WebTools.NET.ContentAnalysis.Models;

using Xunit;

namespace WebTools.NET.ContentAnalysis.Tests;

public class HtmlContentAnalyzerTests
{
    [Fact]
    public void Analyze_RemovesNoisePreservesMeaningfulContentAndExtractsStructuredData()
    {
        // Arrange
        const string html = """
            <!doctype html>
            <html>
              <head>
                <meta property="og:title" content="Plans" />
                <script type="application/ld+json">{"@type":"Product","name":"Pro plan","offers":{"price":"19.99"}}</script>
              </head>
              <body>
                <!-- comment -->
                <nav>Navigation</nav>
                <div class="pricing-card">
                  <h1>Pricing plans</h1>
                  <p>Pro plan: $19.99 per month</p>
                  <a href="/signup">Start trial</a>
                </div>
                <div hidden>Hidden price $999</div>
                <script>var unrelated = true;</script>
              </body>
            </html>
            """;
        var analyzer = new HtmlContentAnalyzer();

        // Act
        var result = analyzer.Analyze(html, sourceUri: new Uri("https://test.example.com/pricing"));

        // Assert
        result.TextBlocks.Select(block => block.Text).Should().Contain("Pricing plans");
        result.TextBlocks.Select(block => block.Text).Should().Contain("Pro plan: $19.99 per month");
        result.TextBlocks.SelectMany(block => block.Links).Should().ContainSingle(link =>
            link.Url == "https://test.example.com/signup");
        result.TextBlocks.Select(block => block.Text).Should().NotContain(text => text.Contains("Navigation"));
        result.TextBlocks.Select(block => block.Text).Should().NotContain(text => text.Contains("Hidden price"));
        result.StructuredData.Should().Contain(record => record.SourceType == "JsonLd" &&
                                                           record.ParseStatus == HtmlStructuredDataParseStatus.Parsed);
        result.StructuredData.Should().Contain(record => record.SourceType == "Metadata");
        result.CandidateRegions.Should().Contain(candidate => candidate.Signals.Contains("currency"));
    }

    [Fact]
    public void Analyze_MalformedStructuredDataDoesNotPreventOrdinaryExtraction()
    {
        // Arrange
        const string html = "<html><head><script type='application/ld+json'>{bad json</script></head>" +
                            "<body><h1>Annual plan</h1><p>€12 per month</p></body></html>";
        var analyzer = new HtmlContentAnalyzer();

        // Act
        var result = analyzer.Analyze(html);

        // Assert
        result.TextBlocks.Select(block => block.Text).Should().Contain("Annual plan");
        result.StructuredData.Should().ContainSingle(record =>
            record.ParseStatus == HtmlStructuredDataParseStatus.Malformed);
        result.Metadata.Diagnostics.Should().Contain(message => message.Contains("Malformed structured data"));
        result.CandidateRegions.Should().NotBeEmpty();
    }

    [Fact]
    public void Analyze_RanksCandidatesDeterministicallyAndAppliesBounds()
    {
        // Arrange
        const string html = """
            <body>
              <section class="pricing-card"><h2>Basic plan</h2><p>$9 per month</p></section>
              <section class="pricing-card"><h2>Pro plan</h2><p>$19 per month</p></section>
              <section class="pricing-card"><h2>Enterprise plan</h2><p>Annual offer</p></section>
            </body>
            """;
        var options = new HtmlAnalysisOptions
        {
            MaxTextBlocks = 2,
            MaxCandidateRegions = 1,
            MaxCandidateFragmentLength = 12,
            NearbyTextWindow = 8
        };
        var analyzer = new HtmlContentAnalyzer();

        // Act
        var first = analyzer.Analyze(html, options);
        var second = analyzer.Analyze(html, options);

        // Assert
        first.CandidateRegions.Should().HaveCount(1);
        first.CandidateRegions[0].Rank.Should().Be(1);
        first.CandidateRegions[0].Text.Length.Should().BeLessThanOrEqualTo(12);
        first.CandidateRegions[0].IsTruncated.Should().BeTrue();
        first.Metadata.ResultsTruncated.Should().BeTrue();
        first.Metadata.OmittedTextBlocks.Should().BeGreaterThan(0);
        first.CandidateRegions.Select(candidate =>
                (candidate.Text, candidate.Score, candidate.SourceLocation.Selector))
            .Should().Equal(second.CandidateRegions.Select(candidate =>
                (candidate.Text, candidate.Score, candidate.SourceLocation.Selector)));
    }

    [Fact]
    public void Analyze_ExtractsDataAttributesAndRespectsInputLimit()
    {
        // Arrange
        const string html = "<body><div data-plan='pro'>Pro plan</div><p>$19 monthly</p></body>";
        var analyzer = new HtmlContentAnalyzer();

        // Act
        var result = analyzer.Analyze(html, new HtmlAnalysisOptions { MaxInputLength = 30 });

        // Assert
        result.Metadata.InputTruncated.Should().BeTrue();
        result.Metadata.AnalyzedInputLength.Should().Be(30);
        result.StructuredData.Should().Contain(record => record.SourceType == "DataAttribute");
    }

    [Fact]
    public void Analyze_RejectsNonPositiveLimits()
    {
        // Arrange
        var analyzer = new HtmlContentAnalyzer();

        // Act
        var act = () => analyzer.Analyze("<p>content</p>", new HtmlAnalysisOptions { MaxCandidateRegions = 0 });

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(HtmlAnalysisOptions.MaxCandidateRegions));
    }
}
