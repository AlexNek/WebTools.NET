using System.Text.Json;

using AngleSharp.Dom;

using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal sealed class HtmlStructuredDataExtractor
{
    public List<HtmlStructuredDataRecord> Extract(
        IDocument document,
        HtmlSourceLocationMap locations,
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
            var property = meta.GetAttribute("property");
            var name = meta.GetAttribute("name");
            var itemprop = meta.GetAttribute("itemprop");
            var metadataKey = !string.IsNullOrWhiteSpace(property)
                ? property
                : !string.IsNullOrWhiteSpace(name)
                    ? name
                    : itemprop;
            var content = meta.GetAttribute("content");
            if (string.IsNullOrWhiteSpace(metadataKey) || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (metadataKey.StartsWith("og:", StringComparison.OrdinalIgnoreCase) ||
                metadataKey.StartsWith("twitter:", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(itemprop))
            {
                AddRecord(meta, "Metadata", $"{metadataKey}={content}", parseJson: false);
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
}
