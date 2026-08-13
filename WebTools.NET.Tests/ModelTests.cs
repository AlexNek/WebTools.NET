using FluentAssertions;

using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class ModelTests
{
    [Fact]
    public void SearchResult_WhenSuccessful_HasExpectedProperties()
    {
        // Arrange
        var items = new List<SearchResultItem>
        {
            new("Title 1", "https://example.com", "Snippet 1"),
            new("Title 2", "https://test.com", "Snippet 2")
        };

        // Act
        var result = new SearchResult(true, items, null);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(2);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SearchResult_WhenFailed_HasErrorMessage()
    {
        // Arrange & Act
        var result = new SearchResult(false, [], "Network timeout");

        // Assert
        result.Success.Should().BeFalse();
        result.Results.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Network timeout");
    }

    [Fact]
    public void UrlCheckResult_WithRedirects_TracksCount()
    {
        // Arrange & Act
        var result = new UrlCheckResult(
            Reachable: true,
            HttpStatus: 200,
            ErrorMessage: null,
            RedirectCount: 3,
            FinalUrl: "https://example.com/final");

        // Assert
        result.Reachable.Should().BeTrue();
        result.RedirectCount.Should().Be(3);
        result.FinalUrl.Should().Be("https://example.com/final");
        result.ProtectionType.Should().BeNull();
    }

    [Fact]
    public void UrlCheckResult_WithProtection_ReportsType()
    {
        // Arrange & Act
        var result = new UrlCheckResult(
            Reachable: false,
            HttpStatus: 403,
            ErrorMessage: "Blocked",
            ProtectionType: "Cloudflare");

        // Assert
        result.Reachable.Should().BeFalse();
        result.ProtectionType.Should().Be("Cloudflare");
    }

    [Fact]
    public void WebContent_WhenSuccessful_HasContent()
    {
        // Arrange & Act
        var content = new WebContent(true, "Hello World", null, "https://example.com");

        // Assert
        content.Success.Should().BeTrue();
        content.Content.Should().Be("Hello World");
        content.ErrorMessage.Should().BeNull();
        content.FinalUrl.Should().Be("https://example.com");
    }

    [Fact]
    public void WebContent_WhenFailed_HasError()
    {
        // Arrange & Act
        var content = new WebContent(false, "", "HTTP 500", "https://example.com/error");

        // Assert
        content.Success.Should().BeFalse();
        content.Content.Should().BeEmpty();
        content.ErrorMessage.Should().Be("HTTP 500");
    }

    [Fact]
    public void SearchResultItem_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var item1 = new SearchResultItem("Title", "https://example.com", "Snippet");
        var item2 = new SearchResultItem("Title", "https://example.com", "Snippet");

        // Act & Assert
        item1.Should().Be(item2);
    }
}
