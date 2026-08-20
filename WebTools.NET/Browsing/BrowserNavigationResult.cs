namespace WebTools.NET.Browsing;

internal sealed record BrowserNavigationResult(
    int Status,
    string InitialUrl,
    string FinalUrl,
    int RedirectCount,
    int ClientRedirectCount,
    bool HasPostInitialDocumentResponse);
