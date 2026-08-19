namespace WebTools.NET.Models;

/// <summary>
/// Actions the browser agent can execute on a page.
/// </summary>
public enum EBrowserOperationType
{
    /// <summary>Navigate to a URL.</summary>
    Navigate,

    /// <summary>Click an interactive element by index.</summary>
    Click,

    /// <summary>Fill a single input element by index with a text value.</summary>
    Fill,

    /// <summary>Fill multiple form fields at once (text, checkbox, combobox).</summary>
    FillForm,

    /// <summary>Select a dropdown option by element index.</summary>
    Select,

    /// <summary>Submit the form containing the element at the given index.</summary>
    Submit,

    /// <summary>Scroll down by one configured viewport height to load lazy content.</summary>
    ScrollDown,

    /// <summary>Scroll up by one configured viewport height.</summary>
    ScrollUp,

    /// <summary>Wait for a CSS selector to appear on the page.</summary>
    WaitFor,

    /// <summary>Navigate back (browser back button).</summary>
    Back,

    /// <summary>Re-read the current page state without performing any interaction.</summary>
    Snapshot
}
