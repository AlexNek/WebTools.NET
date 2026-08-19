namespace WebTools.NET.Abstractions;

/// <summary>
/// Legacy composite browser capability contract retained for source compatibility.
/// Use <see cref="IBrowserSession"/> for new code.
/// </summary>
[Obsolete("Use IBrowserSession instead.")]
public interface IBrowserAgentInteraction :
    IBrowserInteraction,
    IBrowserElementExtractor,
    IBrowserHistoryNavigation,
    IBrowserFormInteraction,
    IBrowserNavigationStatus,
    IBrowserSessionStorage,
    IBrowserScreenshot,
    IBrowserViewport,
    IBrowserPageWaiter
{
}
