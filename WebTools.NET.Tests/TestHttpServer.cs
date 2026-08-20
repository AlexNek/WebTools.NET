using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WebTools.NET.Tests;

internal sealed class TestHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _body;
    private readonly Task _acceptLoop;
    private readonly Func<string, string>? _bodyProvider;
    private readonly Func<string, string?>? _locationProvider;
    private readonly Func<string, HttpStatusCode> _statusProvider;

    private TestHttpServer(
        TcpListener listener,
        string body,
        Func<string, HttpStatusCode> statusProvider,
        Func<string, string?>? locationProvider,
        Func<string, string>? bodyProvider)
    {
        _listener = listener;
        _body = body;
        _statusProvider = statusProvider;
        _locationProvider = locationProvider;
        _bodyProvider = bodyProvider;
        _acceptLoop = AcceptLoopAsync();
    }

    public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

    public static Task<TestHttpServer> StartAsync(
        string body,
        Func<string, HttpStatusCode>? statusProvider = null,
        Func<string, string?>? locationProvider = null,
        Func<string, string>? bodyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return Task.FromResult(
            new TestHttpServer(
                listener,
                body,
                statusProvider ?? (_ => HttpStatusCode.OK),
                locationProvider,
                bodyProvider));
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _listener.Stop();
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            using (client)
            {
                await HandleClientAsync(client).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var requestBuffer = new byte[4096];
        var requestLength = 0;
        while (requestLength < requestBuffer.Length)
        {
            var read = await stream.ReadAsync(
                    requestBuffer.AsMemory(requestLength),
                    _shutdown.Token)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            requestLength += read;
            if (requestLength >= 4 &&
                Encoding.ASCII.GetString(requestBuffer, 0, requestLength).Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                break;
            }
        }

        var requestText = Encoding.ASCII.GetString(requestBuffer, 0, requestLength);
        var requestTarget = requestText
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[0]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ElementAtOrDefault(1) ?? "/";
        var body = _bodyProvider?.Invoke(requestTarget) ?? _body;
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var status = _statusProvider(requestTarget);
        var location = _locationProvider?.Invoke(requestTarget);
        var locationHeader = string.IsNullOrWhiteSpace(location)
            ? ""
            : $"Location: {location}\r\n";
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)status} {status}\r\n{locationHeader}Content-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, _shutdown.Token).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, _shutdown.Token).ConfigureAwait(false);
    }
}
