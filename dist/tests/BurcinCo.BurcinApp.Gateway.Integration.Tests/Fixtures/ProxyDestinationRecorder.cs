using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

internal sealed class ProxyDestinationRecorder : IAsyncDisposable
{
	private static readonly byte[] _routedResponse = Encoding.ASCII.GetBytes(
		"HTTP/1.1 200 OK\r\n" +
		"Content-Type: application/json\r\n" +
		"Content-Length: 15\r\n" +
		"Connection: close\r\n\r\n" +
		"{\"routed\":true}");

	private readonly TcpListener _listener;
	private readonly CancellationTokenSource _shutdown = new();
	private readonly Task _acceptLoop;
	private int _callCount;

	private ProxyDestinationRecorder()
	{
		_listener = new TcpListener(IPAddress.Loopback, 0);
		_listener.Start();
		var endpoint = (IPEndPoint)_listener.LocalEndpoint;
		DestinationAddress = new Uri($"http://127.0.0.1:{endpoint.Port}", UriKind.Absolute);
		_acceptLoop = AcceptCallsAsync();
	}

	public Uri DestinationAddress { get; }

	public int CallCount => Volatile.Read(ref _callCount);

	public static ProxyDestinationRecorder Start() => new();

	public async ValueTask DisposeAsync()
	{
		await _shutdown.CancelAsync().ConfigureAwait(false);
		_listener.Dispose();
		await _acceptLoop.ConfigureAwait(false);
		_shutdown.Dispose();
	}

	private async Task AcceptCallsAsync()
	{
		try
		{
			while (true)
			{
				using var client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
				Interlocked.Increment(ref _callCount);
				await ReadHeadersAsync(client.GetStream(), _shutdown.Token).ConfigureAwait(false);
				await client.GetStream().WriteAsync(_routedResponse, _shutdown.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
		{
			// Expected during async disposal.
		}
		catch (SocketException) when (_shutdown.IsCancellationRequested)
		{
			// Listener disposal may surface as a socket error on the pending accept.
		}
		catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
		{
			// Some platforms surface listener shutdown as disposal instead.
		}
	}

	private static async Task ReadHeadersAsync(
		NetworkStream stream,
		CancellationToken cancellationToken)
	{
		using var request = new MemoryStream();
		var chunk = new byte[1024];
		while (true)
		{
			var bytesRead = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
			if (bytesRead == 0)
			{
				return;
			}

			await request.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
			var requestText = Encoding.ASCII.GetString(
				request.GetBuffer(),
				0,
				checked((int)request.Length));
			if (requestText.Contains("\r\n\r\n", StringComparison.Ordinal))
			{
				return;
			}
		}
	}
}
