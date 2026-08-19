namespace WebTools.NET.Models;

/// <summary>
/// Represents a single command the LLM can issue to the browser agent.
/// </summary>
/// <param name="Type">The action to perform.</param>
/// <param name="ElementIndex">Target element index (1-based) for Click, Fill, Select, Submit.</param>
/// <param name="Value">Text to fill, URL to navigate to, or CSS selector to wait for.</param>
/// <param name="Fields">Array of field values for the FillForm compound action.</param>
/// <param name="TimeoutMs">Optional timeout override in milliseconds.</param>
public sealed record BrowserAction(
    EBrowserActionType Type,
    int? ElementIndex = null,
    string? Value = null,
    IReadOnlyList<FormFieldValue>? Fields = null,
    int? TimeoutMs = null);
