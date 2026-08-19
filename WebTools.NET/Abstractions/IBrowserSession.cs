namespace WebTools.NET.Abstractions;

/// <summary>
/// Composite browser capability contract required by <see cref="WebTools.NET.BrowserSession"/>.
/// Consumers that need only one capability can depend on the smaller capability interfaces
/// instead of this composite contract.
/// </summary>
public interface IBrowserSession : IBrowserAgentInteraction
{
}
