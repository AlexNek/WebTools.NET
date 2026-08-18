using System.Text.RegularExpressions;

namespace WebTools.NET.Internal;

internal static partial class HtmlSanitizer
{
    internal static string RemoveNoiseTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        return NoiseTagRegex().Replace(html, "");
    }

    [GeneratedRegex(
        @"<(script|style|nav|footer|header)\b[^>]*>[\s\S]*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NoiseTagRegex();
}
