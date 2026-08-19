namespace WebTools.NET.Models;

/// <summary>
/// Legacy browser operation names retained for source compatibility.
/// Use <see cref="EBrowserOperationType"/> for new code.
/// </summary>
[Obsolete("Use EBrowserOperationType instead.")]
public enum EBrowserActionType
{
    Navigate,
    Click,
    Fill,
    FillForm,
    Select,
    Submit,
    ScrollDown,
    ScrollUp,
    WaitFor,
    Back,
    Snapshot
}
