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
    internal static int NormalizeStatusAfterChallenge(
        int status,
        bool challengeResolved,
        bool hasPostInitialDocumentResponse) =>
        challengeResolved &&
        !hasPostInitialDocumentResponse &&
        status == HttpStatusHelper.Forbidden
            ? HttpStatusHelper.Ok
            : status;

    internal static int NormalizeStatusAfterChallenge(int status, bool challengeResolved) =>
        NormalizeStatusAfterChallenge(status, challengeResolved, hasPostInitialDocumentResponse: false);

    internal static string NormalizePlaywrightError(
        PlaywrightException ex,
        string notInstalledMessage)
    {
        return ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal)
                   ? notInstalledMessage
                   : ex.Message;
    }

    /// <summary>
    /// Optionally warms up a page before the target navigation. Warm-up failures are ignored,
    /// but cancellation is always propagated.
    /// </summary>
    internal static async Task WarmupAsync(
        IPage page,
        CancellationToken ct,
        string? warmupUrl = null)
    {
        if (string.IsNullOrWhiteSpace(warmupUrl))
        {
            return;
        }

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

    internal static async Task<IResponse?> GotoAsync(
        IPage page,
        string url,
        int gotoTimeoutMs,
        CancellationToken ct)
    {
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
