using FluentAssertions;

using WebTools.NET.Browsing;

using Xunit;

namespace WebTools.NET.Tests;

[Trait("Category", "Integration")]
public class PlaywrightSessionInteractiveElementsTests
{
    [Fact]
    public async Task GetInteractiveElementsAsync_FiltersHiddenAndDisabledControls_AndCreatesUniqueSelectors()
    {
        // Arrange
        const string html = """
            <html><body>
                <form>
                    <input name="query" value="first" aria-label="First">
                    <input name="query" value="second" aria-label="Second">
                    <button id="submit">Submit</button>
                    <input type="submit" name="send" value="Send">
                    <button disabled>Disabled</button>
                    <input style="display:none" name="hidden">
                    <input type="file" name="upload">
                    <input type="radio" name="choice">
                </form>
                <form>
                    <input name="query" value="third" aria-label="Third">
                </form>
            </body></html>
            """;
        await using var session = new PlaywrightSession();
        await session.NavigateAsync($"data:text/html,{Uri.EscapeDataString(html)}");

        // Act
        var elements = await session.GetInteractiveElementsAsync();

        // Assert
        elements.Should().HaveCount(5);
        elements.Should().NotContain(element => element.Text == "Disabled");
        elements.Should().Contain(element => element.Tag == "input" && element.Type == "submit");
        elements.Should().NotContain(element =>
            element.Name == "hidden" || element.Name == "upload" || element.Name == "choice");
        elements.Select(element => element.Index).Should().Equal(1, 2, 3, 4, 5);
        elements.Select(element => element.Selector).Should().OnlyHaveUniqueItems();
        elements.Should().Contain(element => element.Text == "First");
        elements.Should().Contain(element => element.Text == "Second");
        elements.Should().Contain(element => element.Text == "Third");
    }

    [Fact]
    public async Task GetInteractiveElementsAsync_TruncatesLabelsAndTraversesOpenShadowRoots()
    {
        // Arrange
        var longLabel = new string('x', 100);
        var html = $$"""
            <html><body>
                <div id="host"></div>
                <script>
                    const root = document.querySelector('#host').attachShadow({ mode: 'open' });
                    root.innerHTML = '<button id="shadow-action" aria-label="{{longLabel}}"></button><button>Other</button>';
                </script>
            </body></html>
            """;
        await using var session = new PlaywrightSession();
        await session.NavigateAsync($"data:text/html,{Uri.EscapeDataString(html)}");

        // Act
        var elements = await session.GetInteractiveElementsAsync();

        // Assert
        var shadowButtons = elements.Where(element => element.Tag == "button").ToList();
        shadowButtons.Should().HaveCount(2);
        shadowButtons.Select(element => element.Selector).Should().OnlyHaveUniqueItems();
        var shadowButton = shadowButtons.Single(element => element.Text.Length == 80);
        shadowButton.Selector.Should().Contain("shadow-action");
    }

    [Fact]
    public async Task GetInteractiveElementsAsync_EscapedSelectorCanBeUsedForLiveInteraction()
    {
        // Arrange
        const string html = """
            <html><body>
                <button id="save?button" onclick="document.body.dataset.clicked = 'yes'">Save</button>
            </body></html>
            """;
        await using var session = new PlaywrightSession();
        await session.NavigateAsync($"data:text/html,{Uri.EscapeDataString(html)}");
        var element = (await session.GetInteractiveElementsAsync()).Single();

        // Act
        await session.ClickAsync(element.Selector);
        var updatedHtml = await session.GetHtmlAsync();

        // Assert
        element.Selector.Should().Contain("\\?");
        updatedHtml.Should().Contain("data-clicked=\"yes\"");
    }
}
