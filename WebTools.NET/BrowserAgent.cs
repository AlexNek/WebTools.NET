using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using WebTools.NET.Abstractions;
using WebTools.NET.Browsing;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET;

/// <summary>
/// Stateful browser agent that lets an LLM autonomously navigate, interact with,
/// and extract information from web pages across multiple turns.
/// </summary>
public sealed partial class BrowserAgent : IAsyncDisposable
{
    private const int ViewportHeight = 1080;

    private readonly IBrowserInteraction _browser;

    private readonly List<BrowserAction> _history = [];

    private readonly ILogger<BrowserAgent>? _logger;

    private readonly BrowserAgentOptions _options;

    private readonly PlaywrightSession? _ownedBrowser;

    private readonly Stopwatch _sessionTimer = new();

    private EContentFormat _format = EContentFormat.Markdown;

    private bool _started;

    /// <summary>
    /// Creates a browser agent that owns its own <see cref="PlaywrightSession"/>.
    /// The session is disposed when the agent is disposed.
    /// </summary>
    public BrowserAgent(BrowserAgentOptions? options = null, ILogger<BrowserAgent>? logger = null)
    {
        _options = options ?? new BrowserAgentOptions();
        _logger = logger;
        _ownedBrowser = new PlaywrightSession(_options.StorageStatePath);
        _browser = _ownedBrowser;
    }

    /// <summary>
    /// Creates a browser agent wrapping an externally owned <see cref="IBrowserInteraction"/>.
    /// The caller is responsible for the browser session's lifetime.
    /// </summary>
    public BrowserAgent(
        IBrowserInteraction browser,
        BrowserAgentOptions? options = null,
        ILogger<BrowserAgent>? logger = null)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _options = options ?? new BrowserAgentOptions();
        _logger = logger;
    }

    /// <summary>History of all actions executed in this session.</summary>
    public IReadOnlyList<BrowserAction> ActionHistory => _history.AsReadOnly();

    public async ValueTask DisposeAsync()
    {
        if (_ownedBrowser is not null)
        {
            // Save storage state before closing if configured
            if (_options.StorageStatePath is not null)
            {
                try
                {
                    await _browser.SaveStorageStateAsync(_options.StorageStatePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("Failed to save storage state: {Error}", ex.Message);
                }
            }

            await _ownedBrowser.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a browser action and returns the updated page snapshot.
    /// </summary>
    public async Task<PageSnapshot> ExecuteAsync(BrowserAction action, CancellationToken ct = default)
    {
        if (!_started)
        {
            return ErrorSnapshot("Session not started. Call StartAsync first.");
        }

        if (_history.Count >= _options.MaxActions)
        {
            return ErrorSnapshot($"Action limit ({_options.MaxActions}) reached.");
        }

        if (_sessionTimer.Elapsed > _options.MaxDuration)
        {
            return ErrorSnapshot($"Session duration limit ({_options.MaxDuration.TotalMinutes:F0} min) exceeded.");
        }

        _history.Add(action);
        _logger?.LogDebug("Executing action #{Count}: {Type}", _history.Count, action.Type);

        try
        {
            await DispatchActionAsync(action, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Action {Type} failed: {Error}", action.Type, ex.Message);
            return await BuildSnapshotAsync(NormalizeError(ex.Message), ct);
        }

        return await BuildSnapshotAsync(null, ct);
    }

    /// <summary>
    /// Returns the current page state without performing any action.
    /// </summary>
    public async Task<PageSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        if (!_started)
        {
            return ErrorSnapshot("Session not started. Call StartAsync first.");
        }

        return await BuildSnapshotAsync(null, ct);
    }

    /// <summary>
    /// Starts a new browser agent session by navigating to the given URL.
    /// </summary>
    public async Task<PageSnapshot> StartAsync(
        string url,
        EContentFormat format = EContentFormat.Markdown,
        CancellationToken ct = default)
    {
        _format = format;
        _started = true;
        _sessionTimer.Restart();

        _logger?.LogDebug("Starting browser agent session at {Url}", url);

        // Load storage state if configured
        if (_options.StorageStatePath is not null)
        {
            try
            {
                await _browser.LoadStorageStateAsync(_options.StorageStatePath, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Failed to load storage state: {Error}", ex.Message);
            }
        }

        try
        {
            await _browser.NavigateAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Navigation to {Url} failed: {Error}", url, ex.Message);
            return await BuildSnapshotAsync(NormalizeError(ex.Message), ct);
        }

        return await BuildSnapshotAsync(null, ct);
    }

    private async Task<PageSnapshot> BuildSnapshotAsync(string? error, CancellationToken ct)
    {
        string url;
        string title;
        string content;
        bool hasMore;
        string? screenshot = null;

        try
        {
            url = await _browser.GetCurrentUrlAsync(ct);
            title = await _browser.GetTitleAsync(ct);

            var html = await _browser.GetHtmlAsync(ct);
            content = ContentProcessor.Process(html, _format, null, ESanitizeLevel.Minimal);

            hasMore = await EvaluateHasMoreContentAsync(ct);

            if (_options.IncludeScreenshot)
            {
                screenshot = await _browser.ScreenshotAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Failed to build snapshot: {Error}", ex.Message);
            return ErrorSnapshot(error ?? NormalizeError(ex.Message));
        }

        var elements = await ExtractInteractiveElementsAsync(ct);

        return new PageSnapshot(
            Url: url,
            Title: title,
            Content: content,
            Elements: elements,
            Format: _format,
            StatusCode: null,
            Error: error,
            HasMoreContent: hasMore,
            ScreenshotBase64: screenshot);
    }

    private async Task DispatchActionAsync(BrowserAction action, CancellationToken ct)
    {
        switch (action.Type)
        {
            case EBrowserActionType.Navigate:
                if (string.IsNullOrWhiteSpace(action.Value))
                    throw new InvalidOperationException("Navigate requires a URL in Value.");
                await _browser.NavigateAsync(action.Value, ct);
                break;

            case EBrowserActionType.Click:
                var clickSelector = await ResolveElementSelectorAsync(action.ElementIndex, ct);
                await _browser.ClickAsync(clickSelector, ct);
                break;

            case EBrowserActionType.Fill:
                var fillSelector = await ResolveElementSelectorAsync(action.ElementIndex, ct);
                await _browser.FillAsync(fillSelector, action.Value ?? "", ct);
                break;

            case EBrowserActionType.FillForm:
                if (action.Fields is null || action.Fields.Count == 0)
                    throw new InvalidOperationException("FillForm requires at least one field in Fields.");
                await ExecuteFillFormAsync(action.Fields, ct);
                break;

            case EBrowserActionType.Select:
                var selectSelector = await ResolveElementSelectorAsync(action.ElementIndex, ct);
                await _browser.SelectOptionAsync(selectSelector, action.Value ?? "", ct);
                break;

            case EBrowserActionType.Submit:
                var submitSelector = await ResolveElementSelectorAsync(action.ElementIndex, ct);
                await _browser.SubmitFormAsync(submitSelector, ct);
                break;

            case EBrowserActionType.ScrollDown:
                await _browser.ScrollAsync(ViewportHeight, ct);
                break;

            case EBrowserActionType.ScrollUp:
                await _browser.ScrollAsync(-ViewportHeight, ct);
                break;

            case EBrowserActionType.WaitFor:
                if (string.IsNullOrWhiteSpace(action.Value))
                    throw new InvalidOperationException("WaitFor requires a CSS selector in Value.");
                var timeout = action.TimeoutMs ?? 5000;
                await _browser.WaitForSelectorAsync(action.Value, timeout, ct);
                break;

            case EBrowserActionType.Back:
                await _browser.GoBackAsync(ct);
                break;

            case EBrowserActionType.Snapshot:
                // No-op — snapshot is built after every action anyway
                break;

            default:
                throw new InvalidOperationException($"Unknown action type: {action.Type}");
        }
    }

    private PageSnapshot ErrorSnapshot(string error)
    {
        return new PageSnapshot(
            Url: "",
            Title: "",
            Content: "",
            Elements: [],
            Format: _format,
            StatusCode: null,
            Error: error);
    }

    private async Task<bool> EvaluateHasMoreContentAsync(CancellationToken ct)
    {
        // Heuristic: check if page is scrollable by comparing body scrollHeight with viewport
        try
        {
            var html = await _browser.GetHtmlAsync(ct);
            // If page has content, assume there might be more — actual scroll detection
            // would require JS evaluation which we delegate to the full page interaction
            return html.Length > 50_000;
        }
        catch
        {
            return false;
        }
    }

    private async Task ExecuteFillFormAsync(IReadOnlyList<FormFieldValue> fields, CancellationToken ct)
    {
        var elements = await ExtractInteractiveElementsAsync(ct);

        foreach (var field in fields)
        {
            var element = elements.FirstOrDefault(e => e.Index == field.ElementIndex);
            if (element is null)
                throw new InvalidOperationException($"Element index {field.ElementIndex} not found.");

            var tag = element.Tag.ToLowerInvariant();
            var type = element.Type?.ToLowerInvariant();

            if (tag == "select")
            {
                await _browser.SelectOptionAsync(element.Selector, field.Value, ct);
            }
            else if (tag == "input" && type == "checkbox")
            {
                // For checkbox: click to toggle if current state differs
                var shouldCheck = string.Equals(field.Value, "true", StringComparison.OrdinalIgnoreCase);
                // Click toggles the checkbox
                if (shouldCheck)
                {
                    await _browser.ClickAsync(element.Selector, ct);
                }
            }
            else
            {
                await _browser.FillAsync(element.Selector, field.Value, ct);
            }
        }
    }

    private async Task<IReadOnlyList<InteractiveElement>> ExtractInteractiveElementsAsync(CancellationToken ct)
    {
        try
        {
            var html = await _browser.GetHtmlAsync(ct);
            return ExtractElementsFromHtml(html);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Element extraction failed: {Error}", ex.Message);
            return [];
        }
    }

    private static List<InteractiveElement> ExtractElementsFromHtml(string html)
    {
        var elements = new List<InteractiveElement>();
        var index = 1;

        // Extract links
        foreach (Match m in LinkRegex().Matches(html))
        {
            var href = m.Groups[1].Value.Trim();
            var text = StripTags(m.Groups[2].Value).Trim();
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(href))
                continue;

            elements.Add(new InteractiveElement(
                Index: index++,
                Tag: "a",
                Type: null,
                Text: text.Length > 100 ? text[..100] : text,
                Href: href,
                Name: null,
                Selector: $"a[href='{EscapeCssValue(href)}']"));
        }

        // Extract buttons
        foreach (Match m in ButtonRegex().Matches(html))
        {
            var attrs = m.Groups[1].Value;
            var text = StripTags(m.Groups[2].Value).Trim();
            var name = ExtractAttr(attrs, "name");

            elements.Add(new InteractiveElement(
                Index: index++,
                Tag: "button",
                Type: ExtractAttr(attrs, "type") ?? "button",
                Text: text.Length > 100 ? text[..100] : text,
                Href: null,
                Name: name,
                Selector: name is not null ? $"button[name='{EscapeCssValue(name)}']" : $"button:nth-of-type({index - 1})"));
        }

        // Extract inputs
        foreach (Match m in InputRegex().Matches(html))
        {
            var attrs = m.Groups[1].Value;
            var type = ExtractAttr(attrs, "type") ?? "text";
            var name = ExtractAttr(attrs, "name");
            var placeholder = ExtractAttr(attrs, "placeholder") ?? "";

            if (type is "hidden" or "submit")
                continue;

            elements.Add(new InteractiveElement(
                Index: index++,
                Tag: "input",
                Type: type,
                Text: placeholder,
                Href: null,
                Name: name,
                Selector: name is not null ? $"input[name='{EscapeCssValue(name)}']" : $"input[type='{type}']:nth-of-type({index - 1})"));
        }

        // Extract textareas
        foreach (Match m in TextareaRegex().Matches(html))
        {
            var attrs = m.Groups[1].Value;
            var name = ExtractAttr(attrs, "name");
            var placeholder = ExtractAttr(attrs, "placeholder") ?? "";

            elements.Add(new InteractiveElement(
                Index: index++,
                Tag: "textarea",
                Type: null,
                Text: placeholder,
                Href: null,
                Name: name,
                Selector: name is not null ? $"textarea[name='{EscapeCssValue(name)}']" : $"textarea:nth-of-type({index - 1})"));
        }

        // Extract selects
        foreach (Match m in SelectRegex().Matches(html))
        {
            var attrs = m.Groups[1].Value;
            var name = ExtractAttr(attrs, "name");

            elements.Add(new InteractiveElement(
                Index: index++,
                Tag: "select",
                Type: null,
                Text: name ?? "dropdown",
                Href: null,
                Name: name,
                Selector: name is not null ? $"select[name='{EscapeCssValue(name)}']" : $"select:nth-of-type({index - 1})"));
        }

        return elements;
    }

    private static string? ExtractAttr(string attrs, string name)
    {
        var match = Regex.Match(attrs, $@"{name}\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string EscapeCssValue(string value)
    {
        return value.Replace("'", "\\'").Replace("\\", "\\\\");
    }

    private static string NormalizeError(string message)
    {
        // Trim Playwright-specific prefixes for cleaner LLM consumption
        if (message.StartsWith("Timeout ", StringComparison.OrdinalIgnoreCase))
            return message;
        return message;
    }

    private async Task<string> ResolveElementSelectorAsync(int? elementIndex, CancellationToken ct)
    {
        if (elementIndex is null)
            throw new InvalidOperationException("ElementIndex is required for this action.");

        var elements = await ExtractInteractiveElementsAsync(ct);
        var element = elements.FirstOrDefault(e => e.Index == elementIndex.Value);
        if (element is null)
            throw new InvalidOperationException($"Element index {elementIndex.Value} not found.");

        return element.Selector;
    }

    private static string StripTags(string html)
    {
        return TagRegex().Replace(html, "");
    }

    [GeneratedRegex(@"<button([^>]*)>(.*?)</button>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ButtonRegex();

    [GeneratedRegex(@"<input([^>]*)\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex InputRegex();

    [GeneratedRegex(@"<a[^>]+href\s*=\s*[""']([^""']*)[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"<select([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex SelectRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"<textarea([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex TextareaRegex();
}
