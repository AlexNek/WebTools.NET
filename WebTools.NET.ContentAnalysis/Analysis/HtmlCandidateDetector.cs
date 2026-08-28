using System.Text.RegularExpressions;

using AngleSharp.Dom;

using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed class HtmlCandidateDetector
{
    private static readonly string[] SignalOrder =
    [
        "keyword",
        "currency",
        "amount",
        "period",
        "structure",
        "heading",
        "repeated"
    ];

    private static readonly Regex CurrencyPattern = new(
        @"(?:[$€£]\s*\d|\b\d+(?:[.,]\d{1,2})?\s?(?:USD|EUR|GBP|CHF|JPY)\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AmountPattern = new(
        @"\b\d+(?:[.,]\d+)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PeriodPattern = new(
        @"\b(?:month|monthly|year|yearly|annual|annually|week|weekly|day|daily)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public List<HtmlCandidateMatch> Detect(
        IReadOnlyList<(IElement Element, HtmlTextBlock Block)> blocks,
        HtmlAnalysisOptions options)
    {
        var repeatedKeys = FindRepeatedKeys(blocks);
        var candidates = new List<HtmlCandidateMatch>();

        foreach (var entry in blocks)
        {
            var signals = DetectSignals(entry, repeatedKeys, options);
            if (signals.Count == 0)
            {
                continue;
            }

            var score = signals.Sum(signal => options.SignalWeights[signal]);
            candidates.Add(new HtmlCandidateMatch(entry.Block, signals, score));
        }

        return candidates;
    }

    private static HashSet<string> FindRepeatedKeys(
        IReadOnlyList<(IElement Element, HtmlTextBlock Block)> blocks)
    {
        return blocks
            .SelectMany(entry => GetElementAndContainerAncestors(entry.Element)
                .Select(element => (Element: element, Key: HtmlElementMetadata.GetClassAndIdValue(element).Trim())))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Element).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> DetectSignals(
        (IElement Element, HtmlTextBlock Block) entry,
        IReadOnlySet<string> repeatedKeys,
        HtmlAnalysisOptions options)
    {
        var text = entry.Block.Text;
        var keyword = ContainsConfiguredToken(text, options.CandidateKeywords);
        var structural = GetElementAndContainerAncestors(entry.Element)
            .Select(HtmlElementMetadata.GetClassAndIdValue)
            .Any(value => ContainsConfiguredToken(value, options.StructuralTokens));
        var repeated = GetElementAndContainerAncestors(entry.Element)
            .Select(HtmlElementMetadata.GetClassAndIdValue)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(repeatedKeys.Contains);

        var signals = new List<string>();
        AddSignal(signals, "keyword", keyword);
        AddSignal(signals, "currency", CurrencyPattern.IsMatch(text));
        AddSignal(signals, "amount", AmountPattern.IsMatch(text));
        AddSignal(signals, "period", PeriodPattern.IsMatch(text));
        AddSignal(signals, "structure", structural);
        AddSignal(signals, "heading", entry.Block.Kind == HtmlTextBlockKind.Heading && (keyword || structural));
        AddSignal(signals, "repeated", repeated);

        return SignalOrder
            .Where(signals.Contains)
            .Where(signal => options.SignalWeights.TryGetValue(signal, out var weight) && weight != 0d)
            .ToList()
            .AsReadOnly();
    }

    private static void AddSignal(ICollection<string> signals, string signal, bool detected)
    {
        if (detected)
        {
            signals.Add(signal);
        }
    }

    private static IEnumerable<IElement> GetElementAndContainerAncestors(IElement element)
    {
        yield return element;

        var ancestor = element.ParentElement;
        while (ancestor is not null)
        {
            if (HtmlElementClassification.ContainerElements.Contains(ancestor.LocalName))
            {
                yield return ancestor;
            }

            ancestor = ancestor.ParentElement;
        }
    }

    private static bool ContainsConfiguredToken(string value, IReadOnlyList<string> tokens) =>
        tokens.Any(token => !string.IsNullOrWhiteSpace(token) &&
                            value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
