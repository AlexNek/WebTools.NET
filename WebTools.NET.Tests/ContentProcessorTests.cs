using FluentAssertions;

using WebTools.NET.Internal;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class ContentProcessorTests
{
    [Fact]
    public void Process_MarkdownWithAbsoluteUrls_TruncatesAfterUrlExpansion()
    {
        // Arrange
        const string html = "<a href=\"guide\">Guide</a>";
        const string baseUrl = "https://test.example.com/docs/";
        var relative = ContentProcessor.Process(html, EContentFormat.Markdown, null);
        var maxLength = relative.Length + 1;
        var expanded = ContentProcessor.Process(
            html,
            EContentFormat.MarkdownWithAbsoluteUrls,
            null,
            ESanitizeLevel.Strict,
            baseUrl);

        // Act
        var result = ContentProcessor.Process(
            html,
            EContentFormat.MarkdownWithAbsoluteUrls,
            maxLength,
            ESanitizeLevel.Strict,
            baseUrl);

        // Assert
        expanded.Should().Contain("https://test.example.com/docs/guide");
        expanded.Length.Should().BeGreaterThan(maxLength);
        result.Should().Be(expanded[..maxLength]);
    }

    [Fact]
    public void Process_NonMarkdownFormats_DoNotResolveUrls()
    {
        // Arrange
        const string html = "<a href=\"guide\">Guide</a>";
        const string baseUrl = "https://test.example.com/docs/";

        // Act
        var plainText = ContentProcessor.Process(
            html,
            EContentFormat.PlainText,
            null,
            ESanitizeLevel.Strict,
            baseUrl);
        var renderedHtml = ContentProcessor.Process(
            html,
            EContentFormat.Html,
            null,
            ESanitizeLevel.Strict,
            baseUrl);

        // Assert
        plainText.Should().Be("Guide");
        renderedHtml.Should().Contain("href=\"guide\"");
        renderedHtml.Should().NotContain("https://test.example.com/docs/guide");
    }
}
