using Microsoft.Playwright;

namespace WebTools.NET.Internal;

internal static class BrowserHelpers
{
    internal const string GoogleWarmupUrl = "https://www.google.com";

    internal const int WarmupDelayMs = 300;

    internal const int WarmupGotoTimeoutMs = 8000;

    /// <summary>
    /// After a bot challenge (e.g. Cloudflare) resolves, the original response
    /// status is stale — it still reflects the challenge interstitial (403).
    /// A resolved challenge means the page actually loaded.
    /// </summary>
    internal static int NormalizeStatusAfterChallenge(int status, bool challengeResolved) =>
        challengeResolved ? 200 : status;

    internal static string NormalizePlaywrightError(
        PlaywrightException ex,
        string notInstalledMessage)
    {
        return ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal)
                   ? notInstalledMessage
                   : ex.Message;
    }

    /// <summary>
    /// Warms up the page with a Google visit (helps clear cookie-based blocks),
    /// then navigates to the target URL. Warmup failures are ignored.
    /// </summary>
    internal static async Task<IResponse?> WarmupAndGotoAsync(
        IPage page,
        string url,
        int gotoTimeoutMs,
        CancellationToken ct)
    {
        try
        {
            await page.GotoAsync(
                GoogleWarmupUrl,
                new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = WarmupGotoTimeoutMs
                    });
            await Task.Delay(WarmupDelayMs, ct);
        }
        catch
        {
            // warmup failed — proceed anyway
        }

        return await page.GotoAsync(
                   url,
                   new PageGotoOptions
                       {
                           WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = gotoTimeoutMs
                       });
    }
}
