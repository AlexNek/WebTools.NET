using AngleSharp.Html.Parser;

using WebTools.NET.Models;

namespace WebTools.NET.Internal;

internal static class HtmlSanitizer
{
    private static readonly HashSet<string> StrictNoiseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "nav", "footer", "header"
    };

    private static readonly HashSet<string> MinimalNoiseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style"
    };

    internal static string RemoveNoiseTags(string html, ESanitizeLevel level = ESanitizeLevel.Strict)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        if (level == ESanitizeLevel.None)
        {
            return html;
        }

        var tags = level == ESanitizeLevel.Minimal ? MinimalNoiseTags : StrictNoiseTags;

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        var elements = document.Body?.QuerySelectorAll(string.Join(",", tags));
        if (elements is not null)
        {
            foreach (var element in elements.ToList())
            {
                element.Remove();
            }
        }

        return document.Body?.InnerHtml ?? "";
    }
}
