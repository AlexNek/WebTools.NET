using FluentAssertions;

using WebTools.NET.Browsing;

using Xunit;

namespace WebTools.NET.Tests;

/// <summary>
/// Local Playwright integration tests requiring installed browsers. These tests do not
/// access live services and can run in a dedicated browser-enabled CI job.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "BrowserLocal")]
public class PlaywrightBrowserIntegrationTests
{
    [Fact]
    public async Task PlaywrightContentFetcher_FetchesLocalPage()
    {
        // Arrange
        await using var server = await TestHttpServer.StartAsync(
            "<html><body><h1>Test page</h1></body></html>");
        await using var fetcher = new PlaywrightContentFetcher();

        // Act
        var result = await fetcher.FetchAsync(server.Url);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlaywrightContentFetcher_ReachabilityCountsHttpRedirectsSeparately()
    {
        // Arrange
        await using var server = await TestHttpServer.StartAsync(
            "<html><body><h1>Final page</h1></body></html>",
            path => path switch
            {
                "/" => System.Net.HttpStatusCode.Found,
                "/final" => System.Net.HttpStatusCode.OK,
                _ => System.Net.HttpStatusCode.NotFound
            },
            locationProvider: path => path == "/" ? "/final" : null);
        await using var fetcher = new PlaywrightContentFetcher();

        // Act
        var result = await fetcher.CheckReachabilityAsync(server.Url);

        // Assert
        result.Reachable.Should().BeTrue();
        result.FinalUrl.Should().Be(server.Url + "final");
        result.RedirectCount.Should().Be(1);
        result.ClientRedirectCount.Should().Be(0);
    }

    [Fact]
    public async Task PlaywrightContentFetcher_RetryDoesNotCountWarmupRedirects()
    {
        // Arrange
        var targetRequests = 0;
        await using var server = await TestHttpServer.StartAsync(
            "<html><body><h1>Target page</h1></body></html>",
            path => path switch
            {
                "/" => Volatile.Read(ref targetRequests) == 1
                    ? System.Net.HttpStatusCode.Forbidden
                    : System.Net.HttpStatusCode.OK,
                "/warmup" => System.Net.HttpStatusCode.Found,
                "/warmup-final" => System.Net.HttpStatusCode.OK,
                _ => System.Net.HttpStatusCode.NotFound
            },
            locationProvider: path => path == "/warmup" ? "/warmup-final" : null,
            bodyProvider: path =>
            {
                if (path != "/")
                {
                    return "<html><body><h1>Target page</h1></body></html>";
                }

                var requestIndex = Interlocked.Increment(ref targetRequests);
                return requestIndex == 1
                    ? "<html><title>Just a moment</title><body>challenge-platform</body></html>"
                    : "<html><body><h1>Target page</h1></body></html>";
            });
        await using var fetcher = new PlaywrightContentFetcher(
            warmupUrl: server.Url + "warmup");

        // Act
        var result = await fetcher.CheckReachabilityAsync(server.Url);

        // Assert
        result.Reachable.Should().BeTrue();
        result.RedirectCount.Should().Be(0);
        result.ClientRedirectCount.Should().Be(0);
    }

    [Fact]
    public async Task PlaywrightContentFetcher_ReachabilityDetectsClientSideRedirect()
    {
        // Arrange
        await using var server = await TestHttpServer.StartAsync(
            """
            <html>
              <body><h1>Redirecting page</h1></body>
              <script>
                if (window.location.pathname === "/")
                {
                    setTimeout(() => window.location.replace("/final"), 250);
                }
              </script>
            </html>
            """);
        await using var fetcher = new PlaywrightContentFetcher();

        // Act
        var result = await fetcher.CheckReachabilityAsync(server.Url);

        // Assert
        result.Reachable.Should().BeTrue();
        result.HttpStatus.Should().Be(200);
        result.FinalUrl.Should().Be(server.Url + "final");
        result.ClientRedirectCount.Should().Be(1);
    }

    [Fact]
    public async Task PlaywrightContentFetcher_ReachabilityTracksDelayedMultiHopRedirects()
    {
        // Arrange
        await using var server = await TestHttpServer.StartAsync(
            """
            <script>
              if (window.location.pathname === "/")
              {
                  setTimeout(() => window.location.replace("/step1"), 800);
              }
              else if (window.location.pathname === "/step1")
              {
                  setTimeout(() => window.location.replace("/final"), 250);
              }
            </script>
            """);
        await using var fetcher = new PlaywrightContentFetcher();

        // Act
        var result = await fetcher.CheckReachabilityAsync(server.Url);

        // Assert
        result.Reachable.Should().BeTrue();
        result.FinalUrl.Should().Be(server.Url + "final");
        result.ClientRedirectCount.Should().Be(2);
    }

    [Fact]
    public async Task PlaywrightContentFetcher_ReachabilityUsesFinalDocumentStatus()
    {
        // Arrange
        await using var server = await TestHttpServer.StartAsync(
            """
            <html>
              <body><h1>Redirecting page</h1></body>
              <script>
                if (window.location.pathname === "/")
                {
                    setTimeout(() => window.location.replace("/missing"), 250);
                }
              </script>
            </html>
            """,
            path => path == "/missing" ? System.Net.HttpStatusCode.NotFound : System.Net.HttpStatusCode.OK);
        await using var fetcher = new PlaywrightContentFetcher();

        // Act
        var result = await fetcher.CheckReachabilityAsync(server.Url);

        // Assert
        result.Reachable.Should().BeFalse();
        result.HttpStatus.Should().Be(404);
        result.FinalUrl.Should().Be(server.Url + "missing");
        result.ClientRedirectCount.Should().Be(1);
    }

    [Fact]
    public async Task PlaywrightSession_ReachabilityDetectsClientSideRedirect()
    {
        // Arrange
        await using var server = await TestHttpServer.StartAsync(
            """
            <script>
              if (window.location.pathname === "/")
              {
                  setTimeout(() => window.location.replace("/final"), 250);
              }
            </script>
            """);
        await using var session = new PlaywrightSession();

        // Act
        var reachable = await session.CheckReachabilityAsync(server.Url);
        var finalUrl = await session.GetCurrentUrlAsync();

        // Assert
        reachable.Should().BeTrue();
        finalUrl.Should().Be(server.Url + "final");
    }
}
