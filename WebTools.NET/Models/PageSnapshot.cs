namespace WebTools.NET.Models;

/// <summary>
/// Legacy browser snapshot model retained for source compatibility.
/// Use <see cref="BrowserSnapshot"/> for new code.
/// </summary>
[Obsolete("Use BrowserSnapshot instead.")]
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
