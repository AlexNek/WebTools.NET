using AngleSharp.Dom;
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

    internal static string ResolveRelativeUrls(string html, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return html;
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var elements = document.Body?.QuerySelectorAll("[href], [src]");
        if (elements is null)
        {
            return document.Body?.InnerHtml ?? "";
        }

        foreach (var element in elements)
        {
            ResolveAttribute(element, "href", baseUri);
            ResolveAttribute(element, "src", baseUri);
        }

        return document.Body?.InnerHtml ?? "";
    }

    private static void ResolveAttribute(IElement element, string attributeName, Uri baseUri)
    {
        var value = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var isProtocolRelative = value.StartsWith("//", StringComparison.Ordinal);
        if (!isProtocolRelative && Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return;
        }

        if (Uri.TryCreate(baseUri, value, out var absoluteUri))
        {
            element.SetAttribute(attributeName, absoluteUri.ToString());
        }
    }

    /// <summary>
    /// Removes list elements (ul/ol) where every link is a fragment-only anchor (#...)
    /// and there are at least <paramref name="minItems"/> items.
    /// These are navigation tables-of-contents, not content — fragment-only links
    /// are useless without the source page URL.
    /// </summary>
    internal static string RemoveFragmentOnlyLinkLists(
        string html,
        int minItems = 3)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        var lists = document.Body?.QuerySelectorAll("ul, ol");
        if (lists is null)
        {
            return document.Body?.InnerHtml ?? "";
        }

        foreach (var list in lists.ToList())
        {
            var anchors = list.QuerySelectorAll("a[href]");
            if (anchors.Length < minItems)
            {
                continue;
            }

            var allFragmentOnly = anchors.All(a =>
            {
                var href = a.GetAttribute("href");
                return !string.IsNullOrEmpty(href) && href.StartsWith('#');
            });

            if (allFragmentOnly)
            {
                list.Remove();
            }
        }

        return document.Body?.InnerHtml ?? "";
    }
}
