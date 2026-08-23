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

    internal static string Convert(
        string html,
        ESanitizeLevel level = ESanitizeLevel.Strict,
        string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var resolved = HtmlSanitizer.ResolveRelativeUrls(html, baseUrl);
        var sanitized = HtmlSanitizer.RemoveNoiseTags(resolved, level);
        var withoutToc = level == ESanitizeLevel.None
            ? sanitized
            : HtmlSanitizer.RemoveFragmentOnlyLinkLists(sanitized);
        return Converter.Convert(withoutToc).Trim();
    }
}
