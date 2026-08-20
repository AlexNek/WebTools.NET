using System.Net;

using FluentAssertions;

using WebTools.NET;
using WebTools.NET.Models;

using Xunit;

namespace WebTools.NET.Tests;

public class WebAccessServiceTests : IDisposable
{
    private WebAccessService? _sut;

    public void Dispose() => _sut?.Dispose();

    [Fact]
    public async Task CheckReachabilityAsync_ReachableUrl_ReturnsSuccess()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "hello");
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);

        // Act
        var result = await _sut.CheckReachabilityAsync("https://test.example.com/page");

        // Assert
        result.Reachable.Should().BeTrue();
        result.HttpStatus.Should().Be(200);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckReachabilityAsync_NotFoundUrl_ReturnsNotReachable()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, "not found");
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);

        // Act
        var result = await _sut.CheckReachabilityAsync("https://test.example.com/missing");

        // Assert
        result.Reachable.Should().BeFalse();
        result.HttpStatus.Should().Be(404);
        result.ErrorMessage.Should().Contain("404");
    }

    [Fact]
    public async Task CheckReachabilityAsync_RedirectUrl_FollowsRedirectAndReportsCount()
    {
        // Arrange — simulate 2 redirects then a 200
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount <= 2)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.Found);
                resp.Headers.Location = new Uri("https://test.example.com/final");
                return resp;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("done"),
                RequestMessage = request
            };
        });
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);

        // Act
        var result = await _sut.CheckReachabilityAsync("https://test.example.com/start");

        // Assert
        result.Reachable.Should().BeTrue();
        result.RedirectCount.Should().Be(2);
        result.FinalUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CheckReachabilityAsync_RedirectWithoutLocation_ReturnsActualResponseError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Found, "redirect");
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);

        // Act
        var result = await _sut.CheckReachabilityAsync("https://test.example.com/start");

        // Assert
        result.Reachable.Should().BeFalse();
        result.HttpStatus.Should().Be(302);
        result.ErrorMessage.Should().Contain("Location");
        result.RedirectCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckReachabilityAsync_InvalidDomain_ReturnsErrorMessage()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new HttpRequestException("No such host is known."));
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);

        // Act
        var result = await _sut.CheckReachabilityAsync("https://this-domain-definitely-does-not-exist-xyz999.com");

        // Assert
        result.Reachable.Should().BeFalse();
        result.HttpStatus.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CheckReachabilityAsync_CancelledToken_PropagatesCancellation()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(_ =>
            throw new TaskCanceledException("The operation was canceled."));
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.CheckReachabilityAsync("https://test.example.com/slow", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    [Fact]
    public async Task CheckReachabilityAsync_NotModifiedResponse_IsReachable()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotModified, "");
        var httpClient = new HttpClient(handler);
        _sut = new WebAccessService(httpClient);

        // Act
        var result = await _sut.CheckReachabilityAsync("https://test.example.com/cached");

        // Assert
        result.Reachable.Should().BeTrue();
        result.HttpStatus.Should().Be(304);
        result.ErrorMessage.Should().BeNull();
    }
}
