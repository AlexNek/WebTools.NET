namespace WebTools.NET.ContentAnalysis.Analysis;

internal static class HtmlElementClassification
{
    public static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "blockquote", "caption", "dd", "div", "dt", "figcaption",
        "h1", "h2", "h3", "h4", "h5", "h6", "label", "li", "p", "pre", "section",
        "td", "th", "button", "a"
    };

    public static readonly HashSet<string> ContainerElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "article", "div", "section"
    };
}
