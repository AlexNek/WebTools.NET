namespace WebTools.NET.Internal;

/// <summary>
/// Internal DTO for deserializing the result of in-browser element extraction.
/// </summary>
internal sealed class ElementExtractionResult
{
    public string Tag { get; set; } = "";

    public string? Type { get; set; }

    public string Text { get; set; } = "";

    public string? Href { get; set; }

    public string? Name { get; set; }

    public string Selector { get; set; } = "";
}
