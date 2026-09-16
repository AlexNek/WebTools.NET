namespace WebTools.NET.Models;

/// <summary>
/// Result of a browser-backed content fetch.
/// </summary>
/// <param name="Success">Whether the fetch produced usable content.</param>
/// <param name="Content">The fetched content, or an empty string when the fetch failed.</param>
/// <param name="ErrorMessage">A human-readable failure reason, or <c>null</c> on success.</param>
/// <param name="FinalUrl">
/// The browser-reported URL once navigation and the bounded post-load observation window are
/// complete. It is <c>null</c> when navigation never completed, for example on a timeout or a
/// browser failure.
/// </param>
public sealed record WebContent(
    bool Success,
    string Content,
    string? ErrorMessage,
    string? FinalUrl);
