using System.Diagnostics;

using Microsoft.Playwright;

using WebTools.NET.Internal;

namespace WebTools.NET.Browsing;

internal sealed class BrowserNavigationTracker : IDisposable
{
    private const int UrlStabilityWaitMs = 500;

    private readonly IPage _page;
    private readonly object _stateGate = new();
    private readonly List<string> _pendingMainFrameUrls = [];
    private readonly List<(string Url, int Status)> _pendingDocumentResponses = [];
    private bool _initialNavigationMarked;
    private bool _initialDocumentResponseMarked;
    private string _initialUrl = string.Empty;
    private int _initialStatus;
    private string _lastObservedUrl = string.Empty;
    private int _latestDocumentStatus;
    private int _redirectCount;
    private int _clientRedirectCount;
    private bool _hasPostInitialDocumentResponse;
    private bool _disposed;

    public BrowserNavigationTracker(IPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _page.Response += TrackDocumentResponse;
        _page.FrameNavigated += TrackMainFrameNavigation;
    }

    public async Task<BrowserNavigationResult> ObserveAsync(
        string initialUrl,
        int initialStatus,
        int timeoutMs,
        CancellationToken ct)
    {
        MarkInitialNavigation(initialUrl, initialStatus);

        // Keep observing for the full bounded window: client-side redirects can be
        // scheduled after DOMContentLoaded and after the page first becomes idle.
        // Task.Delay is intentional here: it yields the thread while the async
        // operation remains pending, so delayed navigation is observed without
        // blocking an application thread.
        var startedAt = Stopwatch.GetTimestamp();
        while (GetRemainingMilliseconds(startedAt, timeoutMs) > 0)
        {
            ct.ThrowIfCancellationRequested();
            var remainingMs = GetRemainingMilliseconds(startedAt, timeoutMs);
            try
            {
                await _page.WaitForLoadStateAsync(
                        LoadState.NetworkIdle,
                        new PageWaitForLoadStateOptions
                        {
                            Timeout = Math.Min(UrlStabilityWaitMs, remainingMs)
                        })
                    .AwaitWithCancellationAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Continue observing even when the page has persistent network activity.
            }

            remainingMs = GetRemainingMilliseconds(startedAt, timeoutMs);
            if (remainingMs <= 0)
            {
                break;
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(Math.Min(UrlStabilityWaitMs, remainingMs)),
                    ct)
                .ConfigureAwait(false);
        }

        return CreateResult();
    }

    public BrowserNavigationResult CreateCurrentResult(string initialUrl, int initialStatus)
    {
        MarkInitialNavigation(initialUrl, initialStatus);
        return CreateResult();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _page.Response -= TrackDocumentResponse;
        _page.FrameNavigated -= TrackMainFrameNavigation;
    }

    private void MarkInitialNavigation(string initialUrl, int initialStatus)
    {
        var currentUrl = GetCurrentUrl();
        lock (_stateGate)
        {
            if (_initialNavigationMarked)
            {
                return;
            }

            _initialNavigationMarked = true;
            _initialUrl = string.IsNullOrWhiteSpace(initialUrl) ? currentUrl : initialUrl;
            _initialStatus = initialStatus;
            _lastObservedUrl = _initialUrl;
            if (_latestDocumentStatus == 0)
            {
                _latestDocumentStatus = initialStatus;
            }

            var initialNavigationIndex = _pendingMainFrameUrls.FindIndex(
                observedUrl => AreEquivalentUrls(observedUrl, _initialUrl));

            if (initialNavigationIndex >= 0)
            {
                _lastObservedUrl = _initialUrl;
                for (var i = initialNavigationIndex + 1; i < _pendingMainFrameUrls.Count; i++)
                {
                    RecordMainFrameNavigation(_pendingMainFrameUrls[i]);
                }
            }
            else if (!AreEquivalentUrls(currentUrl, _initialUrl))
            {
                _clientRedirectCount = 1;
                _lastObservedUrl = currentUrl;
            }

            _initialDocumentResponseMarked = true;
            var initialDocumentResponseIndex = _pendingDocumentResponses.FindIndex(
                response => AreEquivalentUrls(response.Url, _initialUrl) &&
                            response.Status == _initialStatus);
            if (initialDocumentResponseIndex >= 0)
            {
                _hasPostInitialDocumentResponse =
                    initialDocumentResponseIndex < _pendingDocumentResponses.Count - 1;
            }

            _pendingDocumentResponses.Clear();
            _pendingMainFrameUrls.Clear();
            RecordMainFrameNavigation(currentUrl);
        }
    }

    private BrowserNavigationResult CreateResult()
    {
        var finalUrl = GetCurrentUrl();
        lock (_stateGate)
        {
            if (_initialNavigationMarked &&
                !AreEquivalentUrls(finalUrl, _lastObservedUrl))
            {
                _clientRedirectCount++;
                _lastObservedUrl = finalUrl;
            }

            return new BrowserNavigationResult(
                _latestDocumentStatus,
                _initialUrl,
                finalUrl,
                _redirectCount,
                _clientRedirectCount,
                _hasPostInitialDocumentResponse);
        }
    }

    private void TrackDocumentResponse(object? sender, IResponse response)
    {
        try
        {
            if (response.Frame == _page.MainFrame &&
                string.Equals(response.Request.ResourceType, "document", StringComparison.OrdinalIgnoreCase))
            {
                lock (_stateGate)
                {
                    if (!_initialNavigationMarked)
                    {
                        _pendingDocumentResponses.Add((response.Url, response.Status));
                    }
                    else if (_initialDocumentResponseMarked)
                    {
                        _hasPostInitialDocumentResponse = true;
                    }

                    if (HttpStatusHelper.IsRedirect(response.Status))
                    {
                        _redirectCount++;
                    }

                    _latestDocumentStatus = response.Status;
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void TrackMainFrameNavigation(object? sender, IFrame frame)
    {
        try
        {
            if (frame != _page.MainFrame || string.IsNullOrWhiteSpace(frame.Url))
            {
                return;
            }

            lock (_stateGate)
            {
                if (!_initialNavigationMarked)
                {
                    _pendingMainFrameUrls.Add(frame.Url);
                    return;
                }

                RecordMainFrameNavigation(frame.Url);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void RecordMainFrameNavigation(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            AreEquivalentUrls(url, _lastObservedUrl))
        {
            return;
        }

        _clientRedirectCount++;
        _lastObservedUrl = url;
    }

    private string GetCurrentUrl()
    {
        try
        {
            return _page.Url;
        }
        catch (ObjectDisposedException)
        {
            lock (_stateGate)
            {
                return _lastObservedUrl;
            }
        }
    }

    private static bool AreEquivalentUrls(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        if (!Uri.TryCreate(left, UriKind.Absolute, out var leftUri) ||
            !Uri.TryCreate(right, UriKind.Absolute, out var rightUri))
        {
            return false;
        }

        return string.Equals(leftUri.Scheme, rightUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(leftUri.UserInfo, rightUri.UserInfo, StringComparison.Ordinal) &&
               string.Equals(leftUri.Host, rightUri.Host, StringComparison.OrdinalIgnoreCase) &&
               leftUri.Port == rightUri.Port &&
               string.Equals(leftUri.AbsolutePath, rightUri.AbsolutePath, StringComparison.Ordinal) &&
               string.Equals(leftUri.Query, rightUri.Query, StringComparison.Ordinal) &&
               string.Equals(leftUri.Fragment, rightUri.Fragment, StringComparison.Ordinal);
    }

    private static int GetRemainingMilliseconds(long startedAt, int timeoutMs)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        return Math.Max(0, timeoutMs - (int)Math.Ceiling(elapsedMs));
    }
}
