using Microsoft.Playwright;

using WebTools.NET.Browsing;
using WebTools.NET.Models;

namespace WebTools.NET.Tests;

public sealed class TestBrowserContentFetcher : BrowserContentFetcherBase
{
    public TaskCompletionSource ContextCreationStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ReleaseContextCreation { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool ResourcesDisposed { get; private set; }

    public static WebContent CreateResult(
        string rawBody,
        string finalUrl,
        int status,
        EContentFormat format,
        int? maxContentLength,
        ESanitizeLevel sanitizeLevel) =>
        CreateFetchResult(rawBody, finalUrl, status, format, maxContentLength, sanitizeLevel);

    protected override string BrowserNotInstalledMessage => "Test browser is unavailable.";

    protected override async Task<IBrowserContext> CreateContextAsync(CancellationToken ct)
    {
        ContextCreationStarted.SetResult();
        await ReleaseContextCreation.Task.ConfigureAwait(false);
        throw new InvalidOperationException("Test context creation stopped.");
    }

    protected override Task DisposeBrowserResourcesAsync()
    {
        ResourcesDisposed = true;
        return Task.CompletedTask;
    }
}
