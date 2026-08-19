using WebTools.NET.Abstractions;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Creates a fresh browser session for each independent browser-session workflow.
/// </summary>
public sealed class BrowserSessionFactory : IBrowserSessionFactory, IBrowserAgentSessionFactory
{
    private readonly EBrowserEngine _engine;

    private readonly bool _headless;

    private readonly BrowserSessionOptions? _sessionOptions;

    private readonly string? _storageStatePath;

    /// <summary>Creates a session factory for the selected browser engine.</summary>
    public BrowserSessionFactory(
        EBrowserEngine engine = EBrowserEngine.Playwright,
        bool headless = true,
        string? storageStatePath = null,
        BrowserSessionOptions? sessionOptions = null)
    {
        if (engine is not EBrowserEngine.Playwright and not EBrowserEngine.CloakBrowser)
        {
            throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unknown browser engine.");
        }

        _engine = engine;
        _headless = headless;
        _storageStatePath = storageStatePath;
        _sessionOptions = sessionOptions;
    }

    /// <inheritdoc />
    public IBrowserSession Create() => _engine switch
    {
        EBrowserEngine.Playwright => new PlaywrightSession(_storageStatePath, _headless, _sessionOptions),
        EBrowserEngine.CloakBrowser => new CloakBrowserSession(_storageStatePath, _headless, _sessionOptions),
        _ => throw new InvalidOperationException("Unknown browser engine.")
    };

    IBrowserAgentInteraction IBrowserAgentSessionFactory.Create() => Create();
}
