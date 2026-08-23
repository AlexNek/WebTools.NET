using FluentAssertions;

using WebTools.NET.Internal;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class HtmlSanitizerTests
{
    [Fact]
    public void ResolveRelativeUrls_ResolvesHrefAndSrcAgainstBaseUrl()
    {
        // Arrange
        var html = "<a href=\"/docs\">Root</a>"
                   + "<a href='overview'>Relative</a>"
                   + "<a href = \"?tab=api\">Query</a>"
                   + "<a href=\"#details\">Fragment</a>"
                   + "<img src=\"//cdn.example.com/logo.png\" alt=\"Logo\">";

        // Act
        var result = HtmlSanitizer.ResolveRelativeUrls(html, "https://test.example.com/docs/");

        // Assert
        result.Should().Contain("href=\"https://test.example.com/docs\"");
        result.Should().Contain("href=\"https://test.example.com/docs/overview\"");
        result.Should().Contain("href=\"https://test.example.com/docs/?tab=api\"");
        result.Should().Contain("href=\"https://test.example.com/docs/#details\"");
        result.Should().Contain("src=\"https://cdn.example.com/logo.png\"");
    }

    [Fact]
    public void ResolveRelativeUrls_PreservesAbsoluteSpecialAndUnrelatedAttributes()
    {
        // Arrange
        var html = "<a href=\"https://test.example.com/already\">Absolute</a>"
                   + "<a href=\"mailto:test@example.com\">Mail</a>"
                   + "<a href=\"javascript:void(0)\">Script</a>"
                   + "<img src=\"data:image/png;base64,fake\" srcset=\"image-2x.png 2x\" alt=\"Image\">"
                   + "<a href=\"\">Empty</a>";

        // Act
        var result = HtmlSanitizer.ResolveRelativeUrls(html, "https://test.example.com/docs/");

        // Assert
        result.Should().Contain("https://test.example.com/already");
        result.Should().Contain("mailto:test@example.com");
        result.Should().Contain("javascript:void(0)");
        result.Should().Contain("data:image/png;base64,fake");
        result.Should().Contain("srcset=\"image-2x.png 2x\"");
        result.Should().Contain("href=\"\"");
    }

    [Fact]
    public void ResolveRelativeUrls_InvalidBaseUrl_ReturnsOriginalHtml()
    {
        // Arrange
        var html = "<a href=\"relative\">Link</a>";

        // Act
        var result = HtmlSanitizer.ResolveRelativeUrls(html, "not an absolute URL");

        // Assert
        result.Should().Be(html);
    }

    [Fact]
    public void RemoveNoiseTags_RemovesScriptStyleNavFooterHeader()
    {
        // Arrange
        var html = "<div><p>Keep</p><script>alert(1)</script><style>.x{}</style>" +
                   "<nav>nav</nav><footer>foot</footer><header>head</header></div>";

        // Act
        var result = HtmlSanitizer.RemoveNoiseTags(html);

        // Assert
        result.Should().Contain("<p>Keep</p>");
        result.Should().NotContain("<script");
        result.Should().NotContain("<style");
        result.Should().NotContain("<nav");
        result.Should().NotContain("<footer");
        result.Should().NotContain("<header");
    }

    [Fact]
    public void RemoveNoiseTags_NestedNoiseElements_RemovesAll()
    {
        // Arrange
        var html = "<nav><nav>inner nav</nav><a href=\"/link\">link</a></nav><p>content</p>";

        // Act
        var result = HtmlSanitizer.RemoveNoiseTags(html);

        // Assert
        result.Should().Contain("<p>content</p>");
        result.Should().NotContain("nav");
        result.Should().NotContain("inner");
        result.Should().NotContain("link");
    }

    [Fact]
    public void RemoveNoiseTags_ClosingTagInsideComment_HandledCorrectly()
    {
        // Per HTML spec, <script> content is raw text — </script> always terminates it,
        // even inside a JS comment. This matches real browser behavior.
        // The DOM parser (unlike regex) handles this correctly: the script element
        // contains "/* <!-- " and everything after the first </script> is normal HTML.
        var html = "<script>/* <!-- </script> --> */ var x = 1;<p>visible</p>";

        // Act
        var result = HtmlSanitizer.RemoveNoiseTags(html);

        // Assert — script removed, remainder preserved as the parser sees it
        result.Should().NotContain("<script");
        result.Should().Contain("visible");
    }

    [Fact]
    public void RemoveNoiseTags_PreservesContentTags()
    {
        // Arrange
        var html = "<div><h1>Title</h1><p>Text</p><table><tr><td>data</td></tr></table></div>";

        // Act
        var result = HtmlSanitizer.RemoveNoiseTags(html);

        // Assert
        result.Should().Contain("<h1>Title</h1>");
        result.Should().Contain("<p>Text</p>");
        result.Should().Contain("<table>");
    }

    [Fact]
    public void RemoveNoiseTags_EmptyInput_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        HtmlSanitizer.RemoveNoiseTags("").Should().BeEmpty();
        HtmlSanitizer.RemoveNoiseTags("   ").Should().BeEmpty();
    }

    [Fact]
    public void RemoveNoiseTags_MinimalLevel_KeepsNavHeaderFooter()
    {
        // Arrange
        var html = "<nav>menu</nav><header>head</header><footer>foot</footer>" +
                   "<script>alert(1)</script><style>.x{}</style><p>content</p>";

        // Act
        var result = HtmlSanitizer.RemoveNoiseTags(html, ESanitizeLevel.Minimal);

        // Assert
        result.Should().Contain("menu");
        result.Should().Contain("head");
        result.Should().Contain("foot");
        result.Should().Contain("content");
        result.Should().NotContain("<script");
        result.Should().NotContain("<style");
    }

    [Fact]
    public void RemoveNoiseTags_NoneLevel_ReturnsUnchanged()
    {
        // Arrange
        var html = "<nav>menu</nav><script>alert(1)</script><p>content</p>";

        // Act
        var result = HtmlSanitizer.RemoveNoiseTags(html, ESanitizeLevel.None);

        // Assert
        result.Should().Contain("<nav>");
        result.Should().Contain("<script>");
        result.Should().Contain("<p>content</p>");
    }
}
