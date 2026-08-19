namespace WebTools.NET.Models;

/// <summary>
/// An interactive element extracted from a web page (link, button, input, select, etc.).
/// </summary>
/// <param name="Index">1-based index for this snapshot (ephemeral — changes between actions).</param>
/// <param name="Tag">HTML tag name (a, button, input, select, textarea).</param>
/// <param name="Type">Input type attribute when applicable (text, password, checkbox, etc.), null otherwise.</param>
/// <param name="Text">Visible text or label of the element.</param>
/// <param name="Href">Link target for anchor elements, null for non-links.</param>
/// <param name="Name">Name attribute of the element, null if absent.</param>
/// <param name="Selector">CSS selector that uniquely identifies this element on the page.</param>
public sealed record InteractiveElement(
    int Index,
    string Tag,
    string? Type,
    string Text,
    string? Href,
    string? Name,
    string Selector);
