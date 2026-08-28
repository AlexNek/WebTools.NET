using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed class HtmlCandidateRegionBuilder
{
    public HtmlCandidateRegion Build(
        HtmlCandidateMatch match,
        IReadOnlyList<(AngleSharp.Dom.IElement Element, HtmlTextBlock Block)> blocks,
        int rank,
        HtmlAnalysisOptions options)
    {
        var textBlocks = blocks.Select(item => item.Block).ToList();
        var index = textBlocks.FindIndex(block => block == match.Block);
        var context = GetContext(textBlocks, index, options.NearbyTextWindow);
        var text = LimitText(match.Block.Text, options.MaxCandidateFragmentLength, out var textTruncated);
        var boundedContext = LimitText(context, options.NearbyTextWindow, out var contextTruncated);

        return new HtmlCandidateRegion(
            text,
            boundedContext,
            match.Signals.ToList().AsReadOnly(),
            match.Score,
            rank,
            match.Block.SourceLocation,
            textTruncated || contextTruncated);
    }

    private static string GetContext(IReadOnlyList<HtmlTextBlock> blocks, int index, int window)
    {
        if (index < 0 || blocks.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var length = 0;
        for (var offset = 1; offset <= blocks.Count && length < window; offset++)
        {
            var before = index - offset;
            var after = index + offset;
            if (before >= 0)
            {
                parts.Insert(0, blocks[before].Text);
                length += blocks[before].Text.Length;
            }

            if (after < blocks.Count && length < window)
            {
                parts.Add(blocks[after].Text);
                length += blocks[after].Text.Length;
            }
        }

        return string.Join(" ", parts);
    }

    private static string LimitText(string value, int maxLength, out bool truncated)
    {
        truncated = value.Length > maxLength;
        return truncated ? value[..maxLength] : value;
    }
}
