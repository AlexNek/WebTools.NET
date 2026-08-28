using System.Text.Json;
using System.Text.RegularExpressions;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using WebTools.NET.ContentAnalysis.Abstractions;
using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

/// <summary>
/// Provides deterministic, browser-independent HTML content analysis.
/// </summary>
public sealed class HtmlContentAnalyzer : IHtmlContentAnalyzer
{
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "blockquote", "caption", "dd", "div", "dt", "figcaption",
        "h1", "h2", "h3", "h4", "h5", "h6", "label", "li", "p", "pre", "section",
        "td", "th", "button", "a"
    };

    private static readonly HashSet<string> ContainerElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "article", "div", "section"
    };

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

    private static readonly Regex KeywordPattern = new(
        @"\b(?:price|pricing|plan|subscription|subscribe|billing|checkout|offer|package|premium|enterprise)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HiddenStylePattern = new(
        @"(?:display\s*:\s*none|visibility\s*:\s*hidden)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] TrackingTokens =
    [
        "cookie", "consent", "tracking", "analytics", "advert", "popup", "modal", "newsletter"
    ];

    public HtmlAnalysisResult Analyze(
        string html,
        HtmlAnalysisOptions? options = null,
        Uri? sourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        var effectiveOptions = options ?? new HtmlAnalysisOptions();
        ValidateOptions(effectiveOptions);

        var originalInputLength = html.Length;
        var analyzedHtml = html.Length > effectiveOptions.MaxInputLength
            ? html[..effectiveOptions.MaxInputLength]
            : html;
        var inputTruncated = analyzedHtml.Length != originalInputLength;
        var diagnostics = new List<string>();
        if (inputTruncated)
        {
            diagnostics.Add("Input HTML was truncated to the configured maximum length.");
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(analyzedHtml);
        var locations = CreateLocations(document);
        var structuredData = ExtractStructuredData(
            document,
            locations,
            effectiveOptions,
            diagnostics,
            out var omittedStructuredDataRecords);

        NormalizeDocument(document, effectiveOptions);
        var blocks = ExtractTextBlocks(document, locations, sourceUri);
        var omittedTextBlocks = Math.Max(0, blocks.Count - effectiveOptions.MaxTextBlocks);
        var visibleBlocks = blocks
            .Take(effectiveOptions.MaxTextBlocks)
            .ToList();

        var candidateEntries = DetectCandidates(blocks, locations);
        var omittedCandidateRegions = Math.Max(0, candidateEntries.Count - effectiveOptions.MaxCandidateRegions);
        var candidates = candidateEntries
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Block.DocumentOrder)
            .Take(effectiveOptions.MaxCandidateRegions)
            .Select((entry, index) => CreateCandidate(entry, blocks, index + 1, effectiveOptions))
            .ToList();

        var resultsTruncated = omittedTextBlocks > 0 ||
                               omittedStructuredDataRecords > 0 ||
                               omittedCandidateRegions > 0 ||
                               candidates.Any(candidate => candidate.IsTruncated);
        var metadata = new HtmlAnalysisMetadata(
            originalInputLength,
            analyzedHtml.Length,
            inputTruncated,
            resultsTruncated,
            omittedTextBlocks,
            omittedStructuredDataRecords,
            omittedCandidateRegions,
            diagnostics);

        return new HtmlAnalysisResult(
            visibleBlocks.Select(entry => entry.Block).ToList(),
            structuredData,
            candidates,
            metadata);
    }

    private static void ValidateOptions(HtmlAnalysisOptions options)
    {
        ValidatePositive(options.MaxInputLength, nameof(options.MaxInputLength));
        ValidatePositive(options.MaxTextBlocks, nameof(options.MaxTextBlocks));
        ValidatePositive(options.MaxStructuredDataRecords, nameof(options.MaxStructuredDataRecords));
        ValidatePositive(options.MaxStructuredDataPayloadLength, nameof(options.MaxStructuredDataPayloadLength));
        ValidatePositive(options.MaxCandidateRegions, nameof(options.MaxCandidateRegions));
        ValidatePositive(options.MaxCandidateFragmentLength, nameof(options.MaxCandidateFragmentLength));
        ValidatePositive(options.NearbyTextWindow, nameof(options.NearbyTextWindow));
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be positive.");
        }
    }

    private static Dictionary<IElement, HtmlSourceLocation> CreateLocations(IDocument document)
    {
        var locations = new Dictionary<IElement, HtmlSourceLocation>();
        var order = 0;
        foreach (var element in document.All)
        {
            locations[element] = new HtmlSourceLocation(
                BuildSelector(element),
                element.LocalName,
                order++);
        }

        return locations;
    }

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

    private static List<HtmlStructuredDataRecord> ExtractStructuredData(
        IDocument document,
        IReadOnlyDictionary<IElement, HtmlSourceLocation> locations,
        HtmlAnalysisOptions options,
        ICollection<string> diagnostics,
        out int omitted)
    {
        var records = new List<HtmlStructuredDataRecord>();
        var omittedCount = 0;

        void AddRecord(IElement element, string sourceType, string payload, bool parseJson)
        {
            if (records.Count >= options.MaxStructuredDataRecords)
            {
                omittedCount++;
                return;
            }

            var location = locations[element];
            var trimmed = payload.Trim();
            var isTruncated = trimmed.Length > options.MaxStructuredDataPayloadLength;
            var boundedPayload = isTruncated
                ? trimmed[..options.MaxStructuredDataPayloadLength]
                : trimmed;
            var status = HtmlStructuredDataParseStatus.NotApplicable;
            JsonElement? parsedValue = null;

            if (string.IsNullOrWhiteSpace(boundedPayload))
            {
                status = HtmlStructuredDataParseStatus.Empty;
            }
            else if (parseJson && !isTruncated)
            {
                try
                {
                    using var json = JsonDocument.Parse(boundedPayload);
                    parsedValue = json.RootElement.Clone();
                    status = HtmlStructuredDataParseStatus.Parsed;
                }
                catch (JsonException)
                {
                    status = HtmlStructuredDataParseStatus.Malformed;
                    diagnostics.Add($"Malformed structured data at {location.Selector}.");
                }
            }
            else if (parseJson)
            {
                status = HtmlStructuredDataParseStatus.Malformed;
                diagnostics.Add($"Structured data at {location.Selector} exceeded the configured payload limit.");
            }

            records.Add(new HtmlStructuredDataRecord(
                sourceType,
                location,
                boundedPayload,
                status,
                parsedValue,
                isTruncated));
        }

        foreach (var script in document.All.Where(element => element.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)))
        {
            var type = script.GetAttribute("type") ?? string.Empty;
            var id = script.GetAttribute("id") ?? string.Empty;
            var isJsonLd = type.Equals("application/ld+json", StringComparison.OrdinalIgnoreCase);
            var isApplicationState = type.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                                     id.Contains("next_data", StringComparison.OrdinalIgnoreCase) ||
                                     id.Contains("initial_state", StringComparison.OrdinalIgnoreCase) ||
                                     id.Contains("application_state", StringComparison.OrdinalIgnoreCase);
            if (isJsonLd || isApplicationState)
            {
                AddRecord(script, isJsonLd ? "JsonLd" : "ApplicationState", script.TextContent ?? string.Empty, parseJson: true);
            }
        }

        foreach (var meta in document.All.Where(element => element.LocalName.Equals("meta", StringComparison.OrdinalIgnoreCase)))
        {
            var property = meta.GetAttribute("property") ?? meta.GetAttribute("name");
            var content = meta.GetAttribute("content");
            if (string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (property.StartsWith("og:", StringComparison.OrdinalIgnoreCase) ||
                property.StartsWith("twitter:", StringComparison.OrdinalIgnoreCase) ||
                meta.HasAttribute("itemprop"))
            {
                AddRecord(meta, "Metadata", $"{property}={content}", parseJson: false);
            }
        }

        foreach (var element in document.All)
        {
            foreach (var attribute in element.Attributes.Where(attribute =>
                         attribute.Name.StartsWith("data-", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(attribute.Value))
                {
                    AddRecord(element, "DataAttribute", $"{attribute.Name}={attribute.Value}", parseJson: false);
                }
            }
        }

        omitted = omittedCount;
        return records;
    }

    private static void NormalizeDocument(IDocument document, HtmlAnalysisOptions options)
    {
        var selectors = new List<string>();
        if (options.RemoveScripts)
        {
            selectors.Add("script");
        }

        if (options.RemoveStyles)
        {
            selectors.Add("style");
            selectors.Add("link[rel='stylesheet']");
        }

        if (options.RemoveNavigationRegions)
        {
            selectors.Add("nav");
            selectors.Add("header");
            selectors.Add("footer");
            selectors.Add("aside");
        }

        if (selectors.Count > 0)
        {
            foreach (var element in document.QuerySelectorAll(string.Join(",", selectors)).ToList())
            {
                element.Remove();
            }
        }

        if (options.RemoveHiddenElements || options.RemoveTrackingElements || options.RemoveDecorativeSvg)
        {
            foreach (var element in document.All.ToList())
            {
                if ((options.RemoveHiddenElements && IsHidden(element)) ||
                    (options.RemoveTrackingElements && IsTrackingElement(element)) ||
                    (options.RemoveDecorativeSvg && IsDecorativeSvg(element)))
                {
                    element.Remove();
                }
            }
        }

        if (options.RemoveComments)
        {
            RemoveComments(document);
        }
    }

    private static bool IsHidden(IElement element)
    {
        if (element.HasAttribute("hidden") ||
            element.GetAttribute("aria-hidden")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (HiddenStylePattern.IsMatch(element.GetAttribute("style") ?? string.Empty))
        {
            return true;
        }

        var classes = GetClassAndIdValue(element);
        return classes.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Equals("hidden", StringComparison.OrdinalIgnoreCase) ||
                          token.Equals("visually-hidden", StringComparison.OrdinalIgnoreCase) ||
                          token.Equals("sr-only", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTrackingElement(IElement element)
    {
        var value = GetClassAndIdValue(element);
        return TrackingTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDecorativeSvg(IElement element)
    {
        if (!element.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasAccessibleText = !string.IsNullOrWhiteSpace(element.GetAttribute("aria-label")) ||
                                element.QuerySelector("title") is not null;
        return !hasAccessibleText && string.IsNullOrWhiteSpace(element.TextContent);
    }

    private static string GetClassAndIdValue(IElement element) =>
        $"{element.GetAttribute("class")} {element.GetAttribute("id")}";

    private static void RemoveComments(INode node)
    {
        foreach (var child in node.ChildNodes.ToList())
        {
            if (child is IComment)
            {
                child.Parent?.RemoveChild(child);
            }
            else
            {
                RemoveComments(child);
            }
        }
    }

    private static List<(IElement Element, HtmlTextBlock Block)> ExtractTextBlocks(
        IDocument document,
        IReadOnlyDictionary<IElement, HtmlSourceLocation> locations,
        Uri? sourceUri)
    {
        var blocks = new List<(IElement Element, HtmlTextBlock Block)>();
        foreach (var element in document.All)
        {
            if (!BlockElements.Contains(element.LocalName) ||
                element.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                element.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase) ||
                HasAncestorBlock(element))
            {
                continue;
            }

            if (ContainerElements.Contains(element.LocalName) &&
                element.QuerySelectorAll(string.Join(",", BlockElements.Where(tag => !ContainerElements.Contains(tag))))
                    .Any())
            {
                continue;
            }

            var text = NormalizeText(element.TextContent);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var location = locations[element];
            var block = new HtmlTextBlock(
                text,
                GetBlockKind(element.LocalName),
                location.DocumentOrder,
                location,
                ExtractLinks(element, sourceUri));
            blocks.Add((element, block));
        }

        return blocks;
    }

    private static bool HasAncestorBlock(IElement element)
    {
        if (element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            var ancestor = element.ParentElement;
            while (ancestor is not null)
            {
                if (BlockElements.Contains(ancestor.LocalName) &&
                    !ancestor.LocalName.Equals("div", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                ancestor = ancestor.ParentElement;
            }
        }

        return false;
    }

    private static HtmlTextBlockKind GetBlockKind(string localName) => localName.ToLowerInvariant() switch
    {
        "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => HtmlTextBlockKind.Heading,
        "p" or "blockquote" or "pre" => HtmlTextBlockKind.Paragraph,
        "li" => HtmlTextBlockKind.ListItem,
        "td" or "th" or "caption" => HtmlTextBlockKind.TableCell,
        "label" or "button" => HtmlTextBlockKind.Label,
        "a" => HtmlTextBlockKind.Link,
        "article" or "section" or "div" => HtmlTextBlockKind.Container,
        _ => HtmlTextBlockKind.Other
    };

    private static IReadOnlyList<HtmlLink> ExtractLinks(IElement element, Uri? sourceUri)
    {
        var anchors = new List<IElement>();
        if (element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            anchors.Add(element);
        }

        anchors.AddRange(element.QuerySelectorAll("a[href]"));
        return anchors
            .Select(anchor =>
            {
                var href = anchor.GetAttribute("href") ?? string.Empty;
                var url = sourceUri is not null && Uri.TryCreate(sourceUri, href, out var absolute)
                    ? absolute.ToString()
                    : href;
                return new HtmlLink(NormalizeText(anchor.TextContent), url);
            })
            .Where(link => !string.IsNullOrWhiteSpace(link.Url))
            .Distinct()
            .ToList();
    }

    private static List<(HtmlTextBlock Block, List<string> Signals, double Score)> DetectCandidates(
        IReadOnlyList<(IElement Element, HtmlTextBlock Block)> blocks,
        IReadOnlyDictionary<IElement, HtmlSourceLocation> locations)
    {
        var repeatedKeys = blocks
            .GroupBy(entry =>
            {
                var key = GetClassAndIdValue(entry.Element).Trim();
                return string.IsNullOrWhiteSpace(key) ? entry.Element.LocalName : key;
            }, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<(HtmlTextBlock Block, List<string> Signals, double Score)>();
        foreach (var entry in blocks)
        {
            var signals = new List<string>();
            var text = entry.Block.Text;
            var classAndId = GetClassAndIdValue(entry.Element);
            var structural = classAndId.Contains("pricing", StringComparison.OrdinalIgnoreCase) ||
                             classAndId.Contains("price", StringComparison.OrdinalIgnoreCase) ||
                             classAndId.Contains("plan", StringComparison.OrdinalIgnoreCase) ||
                             classAndId.Contains("subscription", StringComparison.OrdinalIgnoreCase) ||
                             classAndId.Contains("offer", StringComparison.OrdinalIgnoreCase) ||
                             classAndId.Contains("card", StringComparison.OrdinalIgnoreCase);

            if (KeywordPattern.IsMatch(text))
            {
                signals.Add("keyword");
            }

            if (CurrencyPattern.IsMatch(text))
            {
                signals.Add("currency");
            }

            if (AmountPattern.IsMatch(text))
            {
                signals.Add("amount");
            }

            if (PeriodPattern.IsMatch(text))
            {
                signals.Add("period");
            }

            if (structural)
            {
                signals.Add("structure");
            }

            if (entry.Block.Kind == HtmlTextBlockKind.Heading &&
                (KeywordPattern.IsMatch(text) || structural))
            {
                signals.Add("heading");
            }

            var repetitionKey = string.IsNullOrWhiteSpace(classAndId) ? entry.Element.LocalName : classAndId.Trim();
            if (repeatedKeys.Contains(repetitionKey))
            {
                signals.Add("repeated");
            }

            if (signals.Count == 0)
            {
                continue;
            }

            signals = SignalOrder.Where(signals.Contains).ToList();
            var score = signals.Sum(signal => signal switch
            {
                "keyword" => 3d,
                "currency" => 4d,
                "amount" => 1d,
                "period" => 2d,
                "structure" => 2d,
                "heading" => 2d,
                "repeated" => 1d,
                _ => 0d
            });
            candidates.Add((entry.Block, signals, score));
        }

        return candidates;
    }

    private static HtmlCandidateRegion CreateCandidate(
        (HtmlTextBlock Block, List<string> Signals, double Score) entry,
        IReadOnlyList<(IElement Element, HtmlTextBlock Block)> blocks,
        int rank,
        HtmlAnalysisOptions options)
    {
        var index = blocks.ToList().FindIndex(item => item.Block == entry.Block);
        var context = GetContext(blocks.Select(item => item.Block).ToList(), index, options.NearbyTextWindow);
        var text = LimitText(entry.Block.Text, options.MaxCandidateFragmentLength, out var textTruncated);
        var boundedContext = LimitText(context, options.NearbyTextWindow, out var contextTruncated);
        return new HtmlCandidateRegion(
            text,
            boundedContext,
            entry.Signals,
            entry.Score,
            rank,
            entry.Block.SourceLocation,
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

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }
}
