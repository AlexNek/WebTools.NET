using System.Net;

namespace WebTools.NET.Tests;

/// <summary>
/// A fake HTTP message handler for unit testing HTTP-dependent code.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
    {
        _handler = _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
