using AngleSharp.Dom;

using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed class HtmlSourceLocationMap
{
    private readonly Dictionary<IElement, HtmlSourceLocation> _locations;

    public HtmlSourceLocationMap(IDocument document)
    {
        _locations = new Dictionary<IElement, HtmlSourceLocation>();
        var order = 0;
        foreach (var element in document.All)
        {
            _locations[element] = new HtmlSourceLocation(
                BuildSelector(element),
                element.LocalName,
                order++);
        }
    }

    public HtmlSourceLocation this[IElement element] => _locations[element];

    private static string BuildSelector(IElement element)
    {
        var parts = new List<string>();
        IElement? current = element;
        while (current is not null)
        {
            var parent = current.ParentElement;
            if (parent is null)
            {
                parts.Add(current.LocalName);
                break;
            }

            var sameTagSiblings = parent.Children
                .Where(child => child.LocalName.Equals(current.LocalName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var index = sameTagSiblings.IndexOf(current) + 1;
            parts.Add($"{current.LocalName}:nth-of-type({Math.Max(index, 1)})");
            current = parent;
        }

        parts.Reverse();
        return string.Join(" > ", parts);
    }
}
