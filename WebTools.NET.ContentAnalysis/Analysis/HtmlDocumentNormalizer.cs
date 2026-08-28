using System.Text.RegularExpressions;

using AngleSharp.Dom;

using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed class HtmlDocumentNormalizer
{
    private static readonly Regex HiddenStylePattern = new(
        @"(?:display\s*:\s*none|visibility\s*:\s*hidden)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] TrackingTokens =
    [
        "cookie", "consent", "tracking", "analytics", "advert", "popup", "modal", "newsletter"
    ];

    public void Normalize(IDocument document, HtmlAnalysisOptions options)
    {
        var selectors = new List<string>();
        if (options.RemoveScripts)
        {
            selectors.Add("script");
        }

        if (options.RemoveStyles)
        {
            selectors.Add("style");
            selectors.Add("link[rel='stylesheet']");
        }

        if (options.RemoveNavigationRegions)
        {
            selectors.Add("nav");
            selectors.Add("header");
            selectors.Add("footer");
            selectors.Add("aside");
        }

        if (selectors.Count > 0)
        {
            foreach (var element in document.QuerySelectorAll(string.Join(",", selectors)).ToList())
            {
                element.Remove();
            }
        }

        if (options.RemoveHiddenElements || options.RemoveTrackingElements || options.RemoveDecorativeSvg)
        {
            foreach (var element in document.All.ToList())
            {
                if ((options.RemoveHiddenElements && IsHidden(element)) ||
                    (options.RemoveTrackingElements && IsTrackingElement(element)) ||
                    (options.RemoveDecorativeSvg && IsDecorativeSvg(element)))
                {
                    element.Remove();
                }
            }
        }

        if (options.RemoveComments)
        {
            RemoveComments(document);
        }
    }

    private static bool IsHidden(IElement element)
    {
        if (element.HasAttribute("hidden") ||
            element.GetAttribute("aria-hidden")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (HiddenStylePattern.IsMatch(element.GetAttribute("style") ?? string.Empty))
        {
            return true;
        }

        var classes = HtmlElementMetadata.GetClassAndIdValue(element);
        return classes.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Equals("hidden", StringComparison.OrdinalIgnoreCase) ||
                          token.Equals("visually-hidden", StringComparison.OrdinalIgnoreCase) ||
                          token.Equals("sr-only", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTrackingElement(IElement element)
    {
        var value = HtmlElementMetadata.GetClassAndIdValue(element);
        return TrackingTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDecorativeSvg(IElement element)
    {
        if (!element.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasAccessibleText = !string.IsNullOrWhiteSpace(element.GetAttribute("aria-label")) ||
                                element.QuerySelector("title") is not null;
        return !hasAccessibleText && string.IsNullOrWhiteSpace(element.TextContent);
    }

    private static void RemoveComments(INode node)
    {
        foreach (var child in node.ChildNodes.ToList())
        {
            if (child is IComment)
            {
                child.Parent?.RemoveChild(child);
            }
            else
            {
                RemoveComments(child);
            }
        }
    }
}
