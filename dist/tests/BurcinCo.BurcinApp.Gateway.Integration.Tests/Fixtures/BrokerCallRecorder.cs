using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

internal sealed class BrokerCallRecorder : IAsyncDisposable
{
	private static readonly byte[] RoutedResponse = Encoding.ASCII.GetBytes(
		"HTTP/1.1 200 OK\r\n" +
		"Content-Type: application/json\r\n" +
		"Content-Length: 15\r\n" +
		"Connection: close\r\n\r\n" +
		"{\"routed\":true}");

	private readonly TcpListener _listener;
	private readonly CancellationTokenSource _shutdown = new();
	private readonly Task _acceptLoop;
	private int _callCount;
	private string _lastRequest = string.Empty;

	private BrokerCallRecorder()
	{
		_listener = new TcpListener(IPAddress.Loopback, 0);
		_listener.Start();
		var endpoint = (IPEndPoint)_listener.LocalEndpoint;
		ManagementEndpoint = new Uri($"http://127.0.0.1:{endpoint.Port}", UriKind.Absolute);
		_acceptLoop = AcceptCallsAsync();
	}

	public Uri ManagementEndpoint { get; }

	public int CallCount => Volatile.Read(ref _callCount);

	public string LastRequest => Volatile.Read(ref _lastRequest);

	public static BrokerCallRecorder Start() => new();

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
				var stream = client.GetStream();
				Volatile.Write(
					ref _lastRequest,
					await ReadHttpRequestAsync(stream, _shutdown.Token).ConfigureAwait(false));
				await stream.WriteAsync(RoutedResponse, _shutdown.Token).ConfigureAwait(false);
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

	private static async Task<string> ReadHttpRequestAsync(
		NetworkStream stream,
		CancellationToken cancellationToken)
	{
		using var request = new MemoryStream();
		var chunk = new byte[4096];
		var expectedLength = -1L;

		while (true)
		{
			var bytesRead = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
			if (bytesRead == 0)
			{
				break;
			}

			await request.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
			var requestText = Encoding.UTF8.GetString(request.GetBuffer(), 0, checked((int)request.Length));
			var headerEnd = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
			if (headerEnd < 0)
			{
				continue;
			}

			if (expectedLength < 0)
			{
				const string contentLengthHeader = "Content-Length:";
				foreach (var header in requestText[..headerEnd].Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
				{
					if (header.StartsWith(contentLengthHeader, StringComparison.OrdinalIgnoreCase)
						&& long.TryParse(
							header[contentLengthHeader.Length..].Trim(),
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var contentLength))
					{
						expectedLength = headerEnd + 4L + contentLength;
						break;
					}
				}

				if (expectedLength < 0)
				{
					expectedLength = headerEnd + 4L;
				}
			}

			if (request.Length >= expectedLength)
			{
				break;
			}
		}

		return Encoding.UTF8.GetString(request.ToArray());
	}
}
