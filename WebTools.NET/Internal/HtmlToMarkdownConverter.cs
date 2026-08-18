using ReverseMarkdown;

namespace WebTools.NET.Internal;

internal static class HtmlToMarkdownConverter
{
    private static readonly Converter Converter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Drop,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true
    });

    internal static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var sanitized = HtmlSanitizer.RemoveNoiseTags(html);
        return Converter.Convert(sanitized).Trim();
    }
}
