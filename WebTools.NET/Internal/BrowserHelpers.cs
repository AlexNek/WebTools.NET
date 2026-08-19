using Microsoft.Playwright;

namespace WebTools.NET.Internal;

internal static class BrowserHelpers
{
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
    /// Optionally warms up a page, then navigates to the target URL. Warm-up failures are ignored,
    /// but cancellation is always propagated.
    /// </summary>
    internal static async Task<IResponse?> WarmupAndGotoAsync(
        IPage page,
        string url,
        int gotoTimeoutMs,
        CancellationToken ct,
        string? warmupUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(warmupUrl))
        {
            try
            {
                await page.GotoAsync(
                        warmupUrl,
                        new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = WarmupGotoTimeoutMs
                        })
                    .AwaitWithCancellationAsync(ct)
                    .ConfigureAwait(false);
                await Task.Delay(WarmupDelayMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Warm-up failed — proceed anyway.
            }
        }

        return await page.GotoAsync(
                url,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = gotoTimeoutMs
                })
            .AwaitWithCancellationAsync(ct)
            .ConfigureAwait(false);
    }
}
