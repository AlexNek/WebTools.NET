using System.Text.RegularExpressions;

using AngleSharp.Dom;

using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed class HtmlTextBlockExtractor
{
    public List<(IElement Element, HtmlTextBlock Block)> Extract(
        IDocument document,
        HtmlSourceLocationMap locations,
        Uri? sourceUri)
    {
        var blocks = new List<(IElement Element, HtmlTextBlock Block)>();
        foreach (var element in document.All)
        {
            if (!HtmlElementClassification.BlockElements.Contains(element.LocalName) ||
                element.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                element.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase) ||
                HasAncestorBlock(element))
            {
                continue;
            }

            if (HtmlElementClassification.ContainerElements.Contains(element.LocalName) &&
                element.QuerySelectorAll(string.Join(",", HtmlElementClassification.BlockElements
                    .Where(tag => !HtmlElementClassification.ContainerElements.Contains(tag))))
                    .Any())
            {
                continue;
            }

            var text = NormalizeText(element.TextContent);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var location = locations[element];
            var block = new HtmlTextBlock(
                text,
                GetBlockKind(element.LocalName),
                location.DocumentOrder,
                location,
                ExtractLinks(element, sourceUri));
            blocks.Add((element, block));
        }

        return blocks;
    }

    private static bool HasAncestorBlock(IElement element)
    {
        if (element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            var ancestor = element.ParentElement;
            while (ancestor is not null)
            {
                if (HtmlElementClassification.BlockElements.Contains(ancestor.LocalName) &&
                    !HtmlElementClassification.ContainerElements.Contains(ancestor.LocalName))
                {
                    return true;
                }

                ancestor = ancestor.ParentElement;
            }
        }

        return false;
    }

    private static HtmlTextBlockKind GetBlockKind(string localName) => localName.ToLowerInvariant() switch
    {
        "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => HtmlTextBlockKind.Heading,
        "p" or "blockquote" or "pre" => HtmlTextBlockKind.Paragraph,
        "li" => HtmlTextBlockKind.ListItem,
        "td" or "th" or "caption" => HtmlTextBlockKind.TableCell,
        "label" or "button" => HtmlTextBlockKind.Label,
        "a" => HtmlTextBlockKind.Link,
        "article" or "section" or "div" => HtmlTextBlockKind.Container,
        _ => HtmlTextBlockKind.Other
    };

    private static IReadOnlyList<HtmlLink> ExtractLinks(IElement element, Uri? sourceUri)
    {
        var anchors = new List<IElement>();
        if (element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            anchors.Add(element);
        }

        anchors.AddRange(element.QuerySelectorAll("a[href]"));
        return anchors
            .Select(anchor =>
            {
                var href = anchor.GetAttribute("href") ?? string.Empty;
                var url = sourceUri is not null && Uri.TryCreate(sourceUri, href, out var absolute)
                    ? absolute.ToString()
                    : href;
                return new HtmlLink(NormalizeText(anchor.TextContent), url);
            })
            .Where(link => !string.IsNullOrWhiteSpace(link.Url))
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }
}
