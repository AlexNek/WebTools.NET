using Microsoft.Playwright;

using WebTools.NET.Abstractions;
using WebTools.NET.Internal;
using WebTools.NET.Models;

namespace WebTools.NET.Browsing;

/// <summary>
/// Shared fetch pipeline and lifecycle coordination for browser-backed content fetchers.
/// </summary>
public abstract class BrowserContentFetcherBase : IWebContentFetcher
{
    protected const int ErrorContentLimit = 3000;
    protected const int FetchGotoTimeoutMs = 20_000;
    protected const int GotoTimeoutMs = 15_000;
    protected const int NetworkIdleWaitMs = 5_000;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();

    private IBrowserContext? _context;
    private int _disposed;

    /// <summary>Gets the engine-specific installation error message.</summary>
    protected abstract string BrowserNotInstalledMessage { get; }

    /// <summary>Creates the engine-specific browser context.</summary>
    protected abstract Task<IBrowserContext> CreateContextAsync(CancellationToken ct);

    /// <summary>Disposes the engine-specific browser resources after the context closes.</summary>
    protected abstract Task DisposeBrowserResourcesAsync();

    /// <summary>Disposes engine-specific resources owned outside the shared browser lifecycle.</summary>
    protected virtual Task DisposeAdditionalResourcesAsync() => Task.CompletedTask;

    /// <summary>Optional origin used for an engine-specific warm-up before retrying a blocked URL.</summary>
    protected virtual string? WarmupUrl => null;

    /// <summary>Optionally initializes a newly created page, for example with stealth scripts.</summary>
    protected virtual Task InitializePageAsync(IPage page, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Gives an engine the opportunity to resolve a detected bot challenge in another browser.
    /// A null result means that the challenge could not be resolved by this implementation.
    /// </summary>
    protected virtual Task<UrlCheckResult?> TryReachabilityFallbackAsync(
        string url,
        CancellationToken ct) => Task.FromResult<UrlCheckResult?>(null);

    /// <summary>
    /// Gives an engine the opportunity to resolve a detected bot challenge in another browser.
    /// A null result means that the challenge could not be resolved by this implementation.
    /// </summary>
    protected virtual Task<WebContent?> TryFetchFallbackAsync(
        string url,
        EContentFormat format,
        int? maxContentLength,
        ESanitizeLevel sanitizeLevel,
        CancellationToken ct) => Task.FromResult<WebContent?>(null);

    public Task<UrlCheckResult> CheckReachabilityAsync(string url, CancellationToken ct = default) =>
        WithOperationAsync(ct, token => CheckReachabilityCoreAsync(url, token));

    public Task<WebContent> FetchAsync(
        string url,
        int? maxContentLength = null,
        CancellationToken ct = default) =>
        FetchAsAsync(url, EContentFormat.PlainText, maxContentLength, ESanitizeLevel.Strict, ct);

    public Task<WebContent> FetchAsAsync(
        string url,
        EContentFormat format,
        int? maxContentLength = null,
        ESanitizeLevel sanitizeLevel = ESanitizeLevel.Strict,
        CancellationToken ct = default)
    {
        if (maxContentLength is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxContentLength), maxContentLength, "Value must be a positive integer or null.");
        }

        return WithOperationAsync(
            ct,
            token => FetchAsCoreAsync(url, format, maxContentLength, sanitizeLevel, token));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var context = Interlocked.Exchange(ref _context, null);
            await CloseContextAsync(context).ConfigureAwait(false);
            await DisposeBrowserResourcesAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await DisposeAdditionalResourcesAsync().ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
                _operationLock.Dispose();
                _shutdown.Dispose();
                _initLock.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }

    protected async Task<IBrowserContext> GetContextAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        var context = Volatile.Read(ref _context);
        if (context is not null)
        {
            return context;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            context = _context;
            if (context is not null)
            {
                return context;
            }

            context = await CreateContextAsync(ct).ConfigureAwait(false);
            _context = context;
            return context;
        }
        finally
        {
            _initLock.Release();
        }
    }

    protected async Task<IPage> CreatePageAsync(IBrowserContext context, CancellationToken ct)
    {
        var page = await context.NewPageAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
        try
        {
            await InitializePageAsync(page, ct).ConfigureAwait(false);
        }
        catch
        {
            await ClosePageAsync(page).ConfigureAwait(false);
            throw;
        }

        return page;
    }

    protected static async Task<bool> IsBotChallengePageAsync(IPage page, CancellationToken ct = default)
    {
        try
        {
            var title = await page.TitleAsync().AwaitWithCancellationAsync(ct).ConfigureAwait(false);
            if (title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Attention Required", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var body = await page.TextContentAsync("body").AwaitWithCancellationAsync(ct).ConfigureAwait(false) ?? "";
            return body.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("challenge-error-text", StringComparison.OrdinalIgnoreCase);
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    protected static async Task WaitForNetworkIdleAsync(IPage page, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await page.WaitForLoadStateAsync(
                    LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = timeoutMs })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            ct.ThrowIfCancellationRequested();
        }
    }

    protected static async Task<string> GetRawBodyAsync(
        IPage page,
        EContentFormat format,
        CancellationToken ct)
    {
        return format == EContentFormat.PlainText
            ? await page.TextContentAsync("body").AwaitWithCancellationAsync(ct).ConfigureAwait(false) ?? ""
            : await page.InnerHTMLAsync("body").AwaitWithCancellationAsync(ct).ConfigureAwait(false);
    }

    protected static async Task ClosePageAsync(IPage? page)
    {
        if (page is null)
        {
            return;
        }

        try
        {
            await page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
        {
        }
    }

    protected static async Task CloseContextAsync(IBrowserContext? context)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            await context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
        {
        }
    }

    protected static async Task CloseBrowserAsync(IBrowser? browser)
    {
        if (browser is null)
        {
            return;
        }

        try
        {
            await browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or PlaywrightException)
        {
        }
    }

    protected static WebContent CreateFetchResult(
        string rawBody,
        string finalUrl,
        int status,
        EContentFormat format,
        int? maxContentLength,
        ESanitizeLevel sanitizeLevel)
    {
        if (HtmlUtils.IsErrorPageUrl(finalUrl) && HttpStatusHelper.IsSuccess(status))
        {
            var errorText = HtmlUtils.Truncate(HtmlUtils.StripHtml(rawBody), ErrorContentLimit);
            return new WebContent(false, errorText, $"Redirected to error page ({finalUrl})", finalUrl);
        }

        if (HttpStatusHelper.IsNotSuccess(status))
        {
            var errorText = HtmlUtils.Truncate(HtmlUtils.StripHtml(rawBody), ErrorContentLimit);
            return new WebContent(false, errorText, $"HTTP {status}", finalUrl);
        }

        var content = ContentProcessor.Process(rawBody, format, maxContentLength, sanitizeLevel);
        return new WebContent(true, content, null, finalUrl);
    }

    protected static UrlCheckResult CreateReachabilityResult(
        int status,
        string finalUrl,
        int clientRedirectCount = 0)
    {
        if (HttpStatusHelper.IsSuccess(status) && HtmlUtils.IsErrorPageUrl(finalUrl))
        {
            return new UrlCheckResult(
                false,
                status,
                $"Redirected to error page ({finalUrl})",
                FinalUrl: finalUrl,
                ClientRedirectCount: clientRedirectCount);
        }

        return HttpStatusHelper.IsSuccessOrRedirect(status)
            ? new UrlCheckResult(
                true,
                status,
                null,
                FinalUrl: finalUrl,
                ClientRedirectCount: clientRedirectCount)
            : new UrlCheckResult(
                false,
                status,
                $"HTTP {status}",
                FinalUrl: finalUrl,
                ClientRedirectCount: clientRedirectCount);
    }

    protected static int GetClientRedirectCount(string initialUrl, string finalUrl)
    {
        if (string.Equals(initialUrl, finalUrl, StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(initialUrl, UriKind.Absolute, out var initialUri) ||
            !Uri.TryCreate(finalUrl, UriKind.Absolute, out var finalUri))
        {
            return 0;
        }

        return string.Equals(initialUri.Host, finalUri.Host, StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
    }

    private async Task<(IPage Page, int Status, string FinalUrl)> NavigateWithRetryAsync(
        string url,
        int initialTimeoutMs,
        CancellationToken ct)
    {
        var context = await GetContextAsync(ct).ConfigureAwait(false);
        IPage? page = null;
        try
        {
            page = await CreatePageAsync(context, ct).ConfigureAwait(false);
            var response = await page.GotoAsync(
                    url,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = initialTimeoutMs
                    })
                .AwaitWithCancellationAsync(ct)
                .ConfigureAwait(false);

            var status = response?.Status ?? 0;
            var finalUrl = page.Url;
            if (status == HttpStatusHelper.Forbidden)
            {
                await ClosePageAsync(page).ConfigureAwait(false);
                page = await CreatePageAsync(context, ct).ConfigureAwait(false);
                response = await BrowserHelpers.WarmupAndGotoAsync(
                        page, url, GotoTimeoutMs, ct, WarmupUrl)
                    .ConfigureAwait(false);
                status = response?.Status ?? 0;
                finalUrl = page.Url;
            }

            return (page, status, finalUrl);
        }
        catch
        {
            await ClosePageAsync(page).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<UrlCheckResult> CheckReachabilityCoreAsync(string url, CancellationToken ct)
    {
        IPage? page = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ct.ThrowIfCancellationRequested();
            var navigation = await NavigateWithRetryAsync(url, GotoTimeoutMs, ct)
                .ConfigureAwait(false);
            page = navigation.Page;
            var status = navigation.Status;
            var finalUrl = navigation.FinalUrl;

            if (await IsBotChallengePageAsync(page, ct).ConfigureAwait(false))
            {
                var fallback = await TryReachabilityFallbackAsync(url, ct).ConfigureAwait(false);
                return fallback ?? new UrlCheckResult(
                    false,
                    status,
                    "Blocked by bot protection",
                    FinalUrl: finalUrl,
                    ProtectionType: "Cloudflare");
            }

            await WaitForNetworkIdleAsync(page, NetworkIdleWaitMs, ct).ConfigureAwait(false);
            var postIdleUrl = page.Url;
            var clientRedirectCount = GetClientRedirectCount(finalUrl, postIdleUrl);
            ct.ThrowIfCancellationRequested();
            return CreateReachabilityResult(status, postIdleUrl, clientRedirectCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new UrlCheckResult(false, null, "Timed out");
        }
        catch (PlaywrightException ex)
        {
            return new UrlCheckResult(
                false,
                null,
                BrowserHelpers.NormalizePlaywrightError(ex, BrowserNotInstalledMessage));
        }
        finally
        {
            await ClosePageAsync(page).ConfigureAwait(false);
        }
    }

    private async Task<WebContent> FetchAsCoreAsync(
        string url,
        EContentFormat format,
        int? maxContentLength,
        ESanitizeLevel sanitizeLevel,
        CancellationToken ct)
    {
        IPage? page = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ct.ThrowIfCancellationRequested();
            var navigation = await NavigateWithRetryAsync(url, FetchGotoTimeoutMs, ct)
                .ConfigureAwait(false);
            page = navigation.Page;
            var status = navigation.Status;
            var finalUrl = navigation.FinalUrl;

            if (await IsBotChallengePageAsync(page, ct).ConfigureAwait(false))
            {
                var fallback = await TryFetchFallbackAsync(
                        url, format, maxContentLength, sanitizeLevel, ct)
                    .ConfigureAwait(false);
                return fallback ?? new WebContent(false, "", "Blocked by bot protection", finalUrl);
            }

            await WaitForNetworkIdleAsync(page, NetworkIdleWaitMs, ct).ConfigureAwait(false);
            finalUrl = page.Url;
            var rawBody = await GetRawBodyAsync(page, format, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return CreateFetchResult(rawBody, finalUrl, status, format, maxContentLength, sanitizeLevel);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new WebContent(false, "", "Request timed out", url);
        }
        catch (PlaywrightException ex)
        {
            return new WebContent(
                false,
                "",
                BrowserHelpers.NormalizePlaywrightError(ex, BrowserNotInstalledMessage),
                url);
        }
        finally
        {
            await ClosePageAsync(page).ConfigureAwait(false);
        }
    }

    private async Task<T> WithOperationAsync<T>(
        CancellationToken callerToken,
        Func<CancellationToken, Task<T>> operation)
    {
        callerToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _shutdown.Token);
        await _operationLock.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await operation(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
