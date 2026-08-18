using FluentAssertions;

using WebTools.NET.Internal;

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
}
