namespace WebTools.NET.Abstractions;

/// <summary>
/// Legacy factory contract retained for source compatibility.
/// Use <see cref="IBrowserSessionFactory"/> for new code.
/// </summary>
[Obsolete("Use IBrowserSessionFactory instead.")]
public interface IBrowserAgentSessionFactory
{
    /// <summary>
    /// Creates a new, unstarted browser session owned by the caller.
    /// </summary>
    IBrowserAgentInteraction Create();
}
