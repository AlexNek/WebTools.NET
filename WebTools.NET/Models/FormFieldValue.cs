namespace WebTools.NET.Models;

/// <summary>
/// A single field value for the <see cref="EBrowserActionType.FillForm"/> compound action.
/// Supports text inputs, checkboxes (Value = "true"/"false"), and comboboxes (Value = option text).
/// </summary>
/// <param name="ElementIndex">Index of the interactive element to fill (1-based).</param>
/// <param name="Value">Value to set — text for inputs, "true"/"false" for checkboxes, option text for selects.</param>
public sealed record FormFieldValue(int ElementIndex, string Value);
