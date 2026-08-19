using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Legacy browser-session factory retained for source compatibility.
/// Use <see cref="BrowserSessionFactory"/> for new code.
/// </summary>
[Obsolete("Use BrowserSessionFactory instead.")]
public sealed class BrowserAgentSessionFactory : IBrowserAgentSessionFactory
{
    private readonly BrowserSessionFactory _inner;

    public BrowserAgentSessionFactory(
        EBrowserEngine engine = EBrowserEngine.Playwright,
        bool headless = true,
        string? storageStatePath = null,
        BrowserSessionOptions? sessionOptions = null)
    {
        _inner = new BrowserSessionFactory(engine, headless, storageStatePath, sessionOptions);
    }

    public IBrowserAgentInteraction Create() => _inner.Create();
}
