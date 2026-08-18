using ReverseMarkdown;

using WebTools.NET.Models;

namespace WebTools.NET.Internal;

internal static class HtmlToMarkdownConverter
{
    private static readonly Converter Converter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true
    });

    internal static string Convert(string html, ESanitizeLevel level = ESanitizeLevel.Strict)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var sanitized = HtmlSanitizer.RemoveNoiseTags(html, level);
        return Converter.Convert(sanitized).Trim();
    }
}
