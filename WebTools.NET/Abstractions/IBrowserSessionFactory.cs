namespace WebTools.NET.Abstractions;

/// <summary>
/// Creates isolated browser sessions for independent <see cref="WebTools.NET.BrowserSession"/> workflows.
/// </summary>
public interface IBrowserSessionFactory
{
    /// <summary>
    /// Creates a new, unstarted browser session.
    /// The caller owns and must dispose the returned session.
    /// </summary>
    IBrowserSession Create();
}
