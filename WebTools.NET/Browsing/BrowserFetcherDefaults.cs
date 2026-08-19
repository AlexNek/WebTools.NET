using Microsoft.Playwright;

namespace WebTools.NET.Browsing;

internal static class BrowserFetcherDefaults
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    internal static BrowserNewContextOptions CreateContextOptions() => new()
    {
        UserAgent = BrowserUserAgent,
        Locale = "en-US",
        TimezoneId = "America/New_York",
        ViewportSize = new ViewportSize
        {
            Width = BrowserSessionBase.DefaultViewportWidth,
            Height = BrowserSessionBase.DefaultViewportHeight
        },
        BypassCSP = true,
        JavaScriptEnabled = true
    };
}
