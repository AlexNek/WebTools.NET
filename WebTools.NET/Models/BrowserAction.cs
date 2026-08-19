namespace WebTools.NET.Models;

/// <summary>
/// Legacy browser operation model retained for source compatibility.
/// Use <see cref="BrowserOperation"/> for new code.
/// </summary>
[Obsolete("Use BrowserOperation instead.")]
public sealed record BrowserAction(
    EBrowserActionType Type,
    int? ElementIndex = null,
    string? Value = null,
    IReadOnlyList<FormFieldValue>? Fields = null,
    int? TimeoutMs = null);
