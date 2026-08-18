using WebTools.NET.Models;

namespace WebTools.NET.Internal;

internal static class ContentProcessor
{
    internal static string Process(string rawBody, EContentFormat format, int? maxContentLength, ESanitizeLevel sanitizeLevel = ESanitizeLevel.Strict)
    {
        var result = format switch
        {
            EContentFormat.PlainText => HtmlUtils.StripHtml(rawBody),
            EContentFormat.Markdown => HtmlToMarkdownConverter.Convert(rawBody, sanitizeLevel),
            EContentFormat.Html => HtmlSanitizer.RemoveNoiseTags(rawBody, sanitizeLevel),
            _ => HtmlUtils.StripHtml(rawBody)
        };

        return HtmlUtils.TruncateIfNeeded(result, maxContentLength);
    }
}
