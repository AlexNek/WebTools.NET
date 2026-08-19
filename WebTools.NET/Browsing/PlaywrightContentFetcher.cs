using Microsoft.Playwright;

using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

public sealed class PlaywrightContentFetcher : BrowserContentFetcherBase
{
    private const int ChallengeWaitMs = 30_000;
    private const int FallbackNetworkIdleWaitMs = 10_000;
    private const int FallbackTimeoutMs = 90_000;

    private const string ChallengeWaitScript = """
        () => {
            const title = (document.title || '').toLowerCase();
            const body = (document.body?.textContent || '').toLowerCase();
            return !title.includes('just a moment') &&
                   !title.includes('checking your browser') &&
                   !title.includes('attention required') &&
                   !body.includes('cf-browser-verification') &&
                   !body.includes('challenge-platform') &&
                   !body.includes('challenge-error-text');
        }
        """;

    private readonly bool _allowVisibleFallback;
    private readonly SemaphoreSlim _fallbackLock = new(1, 1);
    private readonly bool _headless;
    private readonly string? _warmupUrl;

    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public PlaywrightContentFetcher(
        bool headless = true,
        bool allowVisibleFallback = false,
        string? warmupUrl = null)
    {
        _headless = headless;
        _allowVisibleFallback = allowVisibleFallback;
        _warmupUrl = warmupUrl;
    }

    protected override string? WarmupUrl => _warmupUrl;

    protected override string BrowserNotInstalledMessage =>
        "Playwright browsers not installed. Run: playwright install (or .\\playwright.ps1 install)";

    protected override async Task<IBrowserContext> CreateContextAsync(CancellationToken ct)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        try
        {
            playwright = await Playwright.CreateAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            browser = await playwright.Chromium
                .LaunchAsync(CreateLaunchOptions(_headless))
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            var context = await browser.NewContextAsync(BrowserFetcherDefaults.CreateContextOptions())
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            _playwright = playwright;
            _browser = browser;
            return context;
        }
        catch
        {
            await CloseBrowserAsync(browser).ConfigureAwait(false);
            playwright?.Dispose();
            throw;
        }
    }

    protected override Task InitializePageAsync(IPage page, CancellationToken ct)
    {
        return InitializePageCoreAsync(page, ct);
    }

    protected override async Task DisposeBrowserResourcesAsync()
    {
        var browser = Interlocked.Exchange(ref _browser, null);
        var playwright = Interlocked.Exchange(ref _playwright, null);
        await CloseBrowserAsync(browser).ConfigureAwait(false);
        playwright?.Dispose();
    }

    protected override Task DisposeAdditionalResourcesAsync()
    {
        _fallbackLock.Dispose();
        return Task.CompletedTask;
    }

    protected override async Task<UrlCheckResult?> TryReachabilityFallbackAsync(
        string url,
        CancellationToken ct)
    {
        if (!_allowVisibleFallback)
        {
            return null;
        }

        return await WithNonHeadlessBrowserAsync<UrlCheckResult?>(
                url,
                ct,
                (page, status, finalUrl, challengeDetected, challengeResolved) =>
                {
                    if (challengeDetected && challengeResolved)
                    {
                        status = 200;
                    }

                    return Task.FromResult<UrlCheckResult?>(
                        challengeDetected && !challengeResolved
                            ? new UrlCheckResult(
                                false,
                                status,
                                "Blocked by bot protection",
                                FinalUrl: finalUrl,
                                ProtectionType: "Cloudflare")
                            : CreateReachabilityResult(status, finalUrl));
                })
            .ConfigureAwait(false);
    }

    protected override async Task<WebContent?> TryFetchFallbackAsync(
        string url,
        EContentFormat format,
        int? maxContentLength,
        ESanitizeLevel sanitizeLevel,
        CancellationToken ct)
    {
        if (!_allowVisibleFallback)
        {
            return null;
        }

        return await WithNonHeadlessBrowserAsync<WebContent?>(
                url,
                ct,
                async (page, status, finalUrl, challengeDetected, challengeResolved) =>
                {
                    if (challengeDetected && !challengeResolved)
                    {
                        return new WebContent(false, "", "Blocked by bot protection", finalUrl);
                    }

                    if (challengeDetected && challengeResolved)
                    {
                        status = 200;
                    }

                    await WaitForNetworkIdleAsync(page, FallbackNetworkIdleWaitMs, ct)
                        .ConfigureAwait(false);
                    finalUrl = page.Url;
                    var rawBody = await GetRawBodyAsync(page, format, ct).ConfigureAwait(false);
                    return CreateFetchResult(
                        rawBody,
                        finalUrl,
                        status,
                        format,
                        maxContentLength,
                        sanitizeLevel);
                })
            .ConfigureAwait(false);
    }

    private static BrowserTypeLaunchOptions CreateLaunchOptions(bool headless) => new()
    {
        Headless = headless,
        Args =
        [
            "--disable-blink-features=AutomationControlled",
            "--disable-extensions",
            "--disable-infobars",
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-dev-shm-usage",
            "--disable-renderer-backgrounding",
            "--disable-backgrounding-occluded-windows"
        ]
    };

    private async Task InitializePageCoreAsync(IPage page, CancellationToken ct)
    {
        try
        {
            await page.AddInitScriptAsync(StealthScripts.ForMode(_headless))
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // Stealth injection is best effort; page navigation can still proceed.
        }
    }

    private async Task<T> WithNonHeadlessBrowserAsync<T>(
        string url,
        CancellationToken ct,
        Func<IPage, int, string, bool, bool, Task<T>> actionAsync)
    {
        await _fallbackLock.WaitAsync(ct).ConfigureAwait(false);
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;
        IPage? page = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            playwright = await Playwright.CreateAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            browser = await playwright.Chromium
                .LaunchAsync(CreateLaunchOptions(headless: false))
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            context = await browser.NewContextAsync(BrowserFetcherDefaults.CreateContextOptions())
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
            page = await context.NewPageAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);

            try
            {
                await page.AddInitScriptAsync(StealthScripts.Minimal)
                    .AwaitWithCancellationAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Minimal stealth is best effort for the visible fallback.
            }

            var response = await page.GotoAsync(
                    url,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = FallbackTimeoutMs
                    })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;
            var challengeDetected = await IsBotChallengePageAsync(page, ct).ConfigureAwait(false);
            var challengeResolved = false;

            if (challengeDetected)
            {
                try
                {
                    await page.WaitForFunctionAsync(
                            ChallengeWaitScript,
                            new PageWaitForFunctionOptions { Timeout = ChallengeWaitMs })
                        .AwaitWithCancellationAsync(ct)
                        .ConfigureAwait(false);
                    challengeResolved = true;
                }
                catch (TimeoutException)
                {
                    // Challenge did not resolve within the configured window.
                }

                finalUrl = page.Url;
                challengeResolved &= !await IsBotChallengePageAsync(page, ct).ConfigureAwait(false);
            }

            finalUrl = page.Url;
            return await actionAsync(
                    page,
                    status,
                    finalUrl,
                    challengeDetected,
                    challengeResolved)
                .ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await ClosePageAsync(page).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await CloseContextAsync(context).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        await CloseBrowserAsync(browser).ConfigureAwait(false);
                    }
                    finally
                    {
                        try
                        {
                            playwright?.Dispose();
                        }
                        finally
                        {
                            _fallbackLock.Release();
                        }
                    }
                }
            }
        }
    }
}
