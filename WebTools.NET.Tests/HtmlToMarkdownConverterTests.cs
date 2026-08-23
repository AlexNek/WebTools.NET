using FluentAssertions;

using WebTools.NET.Internal;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class HtmlToMarkdownConverterTests
{
    [Fact]
    public void Convert_CustomElements_PreservesInnerText()
    {
        // Arrange
        var html = "<my-widget><span>important text</span></my-widget><p>paragraph</p>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert
        result.Should().Contain("important text");
        result.Should().Contain("paragraph");
        result.Should().NotContain("<my-widget");
    }

    [Fact]
    public void Convert_HeadingsAndBold_PreservesMarkdown()
    {
        // Arrange
        var html = "<h1>Title</h1><p>Some <strong>bold</strong> text</p>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert
        result.Should().Contain("# Title");
        result.Should().Contain("**bold**");
    }

    [Fact]
    public void Convert_Table_PreservesStructure()
    {
        // Arrange
        var html = "<table><tr><th>Name</th><th>Value</th></tr><tr><td>A</td><td>1</td></tr></table>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert
        result.Should().Contain("Name");
        result.Should().Contain("Value");
        result.Should().Contain("|");
    }

    [Fact]
    public void Convert_Images_ProducesMarkdownSyntax()
    {
        // Arrange
        var html = "<img src=\"https://test.example.com/logo.png\" alt=\"Logo\" />";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert
        result.Should().Contain("![Logo]");
        result.Should().Contain("https://test.example.com/logo.png");
    }

    [Fact]
    public void Convert_StripsNoiseTags_BeforeConversion()
    {
        // Arrange
        var html = "<nav>menu</nav><p>content</p><script>alert(1)</script>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert
        result.Should().Contain("content");
        result.Should().NotContain("menu");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void Convert_EmptyInput_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        HtmlToMarkdownConverter.Convert("").Should().BeEmpty();
        HtmlToMarkdownConverter.Convert("   ").Should().BeEmpty();
    }

    [Fact]
    public void Convert_MarkdownWithAbsoluteUrls_ResolvesRelativeLinksAndImages()
    {
        // Arrange
        var html = "<p><a href=\"/docs/overview\">Docs</a></p>"
                   + "<p><a href=\"guide\">Guide</a></p>"
                   + "<img src=\"images/logo.png\" alt=\"Logo\">";

        // Act
        var result = HtmlToMarkdownConverter.Convert(
            html,
            ESanitizeLevel.Strict,
            "https://test.example.com/docs/");

        // Assert
        result.Should().Contain("https://test.example.com/docs/overview");
        result.Should().Contain("https://test.example.com/docs/guide");
        result.Should().Contain("https://test.example.com/docs/images/logo.png");
    }

    [Fact]
    public void Convert_Markdown_PreservesRelativeUrls()
    {
        // Arrange
        var html = "<a href=\"/docs/overview\">Docs</a>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert
        result.Should().Contain("/docs/overview");
        result.Should().NotContain("https://test.example.com/docs/overview");
    }

    [Fact]
    public void Convert_NoneSanitizeLevel_PreservesFragmentOnlyLinkList()
    {
        // Arrange
        var html = "<ul>"
                   + "<li><a href=\"#one\">One</a></li>"
                   + "<li><a href=\"#two\">Two</a></li>"
                   + "<li><a href=\"#three\">Three</a></li>"
                   + "</ul>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html, ESanitizeLevel.None);

        // Assert
        result.Should().Contain("One");
        result.Should().Contain("Two");
        result.Should().Contain("Three");
    }

    [Fact]
    public void Convert_MarkdownWithAbsoluteUrls_PreservesFragmentOnlyListAfterResolution()
    {
        // Arrange
        var html = "<ul>"
                   + "<li><a href=\"#one\">One</a></li>"
                   + "<li><a href=\"#two\">Two</a></li>"
                   + "<li><a href=\"#three\">Three</a></li>"
                   + "</ul>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(
            html,
            ESanitizeLevel.Strict,
            "https://test.example.com/docs/");

        // Assert
        result.Should().Contain("One");
        result.Should().Contain("https://test.example.com/docs/#one");
        result.Should().Contain("Three");
    }

    [Fact]
    public void Convert_FragmentOnlyLinkList_StripsTocNavigation()
    {
        // Arrange
        var html = "<p>Page content here.</p>"
                   + "<ul>"
                   + "<li><a href=\"#section-one\">Section One</a></li>"
                   + "<li><a href=\"#section-two\">Section Two</a></li>"
                   + "<li><a href=\"#section-three\">Section Three</a></li>"
                   + "</ul>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert — content preserved, fragment-only TOC removed
        result.Should().Contain("Page content here.");
        result.Should().NotContain("#section-one");
        result.Should().NotContain("Section One");
    }

    [Fact]
    public void Convert_FragmentOnlyListWithFewerThanThreeItems_KeepsList()
    {
        // Arrange — only 2 fragment links, below threshold
        var html = "<p>Content</p>"
                   + "<ul>"
                   + "<li><a href=\"#faq\">FAQ</a></li>"
                   + "<li><a href=\"#contact\">Contact</a></li>"
                   + "</ul>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert — list preserved (below 3-item threshold)
        result.Should().Contain("Content");
        result.Should().Contain("FAQ");
        result.Should().Contain("Contact");
    }

    [Fact]
    public void Convert_MixedLinkList_PreservesList()
    {
        // Arrange — list has both fragment and absolute links
        var html = "<ul>"
                   + "<li><a href=\"#top\">Back to top</a></li>"
                   + "<li><a href=\"https://test.example.com/docs\">Docs</a></li>"
                   + "<li><a href=\"https://test.example.com/api\">API</a></li>"
                   + "</ul>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert — list preserved because not all links are fragment-only
        result.Should().Contain("https://test.example.com/docs");
        result.Should().Contain("https://test.example.com/api");
    }

    [Fact]
    public void Convert_AbsoluteUrlLinkList_PreservesList()
    {
        // Arrange — all links are absolute URLs
        var html = "<ul>"
                   + "<li><a href=\"https://test.example.com/a\">Link A</a></li>"
                   + "<li><a href=\"https://test.example.com/b\">Link B</a></li>"
                   + "<li><a href=\"https://test.example.com/c\">Link C</a></li>"
                   + "</ul>";

        // Act
        var result = HtmlToMarkdownConverter.Convert(html);

        // Assert — list preserved because links are absolute
        result.Should().Contain("https://test.example.com/a");
        result.Should().Contain("https://test.example.com/b");
        result.Should().Contain("https://test.example.com/c");
    }
}
