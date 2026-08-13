using System.Text.RegularExpressions;

namespace WebTools.NET.Internal;

internal static partial class HtmlUtils
{
    internal static bool IsErrorPageUrl(string url) =>
        url.Contains("/notfound", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/error", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/404", StringComparison.OrdinalIgnoreCase);

    internal static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        return WhitespaceRegex().Replace(TagRegex().Replace(html, " "), " ").Trim();
    }

    internal static string StripTags(string html) => TagRegex().Replace(html, " ").Trim();

    internal static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "\n... [truncated]";

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
