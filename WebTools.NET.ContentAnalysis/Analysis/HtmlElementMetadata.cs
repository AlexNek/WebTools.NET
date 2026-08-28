using AngleSharp.Dom;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal static class HtmlElementMetadata
{
    public static string GetClassAndIdValue(IElement element) =>
        $"{element.GetAttribute("class")} {element.GetAttribute("id")}";
}
