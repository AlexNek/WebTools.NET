namespace WebTools.NET.Models;

/// <summary>
/// The complete observable state of a browser page after an agent action.
/// The LLM receives this after every action and uses it to decide the next step.
/// </summary>
/// <param name="Url">Current page URL (after any redirects).</param>
/// <param name="Title">Page title.</param>
/// <param name="Content">Page content formatted per <paramref name="Format"/>.</param>
/// <param name="Elements">Interactive elements on the page (clickable, fillable, selectable).</param>
/// <param name="Format">Content format used for <paramref name="Content"/>.</param>
/// <param name="StatusCode">HTTP status code of the last navigation, null if unavailable.</param>
/// <param name="Error">Error description when the last action failed, null on success.</param>
/// <param name="HasMoreContent">True when the page has more content below the current scroll position.</param>
/// <param name="ScreenshotBase64">Base64-encoded PNG screenshot when opted in via options, null otherwise.</param>
public sealed record PageSnapshot(
    string Url,
    string Title,
    string Content,
    IReadOnlyList<InteractiveElement> Elements,
    EContentFormat Format,
    int? StatusCode,
    string? Error,
    bool HasMoreContent = false,
    string? ScreenshotBase64 = null);
