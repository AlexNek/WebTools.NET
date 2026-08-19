namespace WebTools.NET.Abstractions;

/// <summary>
/// Reports whether a browser session currently has a usable page and context.
/// </summary>
public interface IBrowserSessionState
{
    /// <summary>Gets whether the session has successfully initialized a page.</summary>
    bool IsPageReady { get; }
}
