using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

internal sealed class NoContentLengthHttpContent : HttpContent
{
	private readonly byte[] _payload;

	public NoContentLengthHttpContent(string payload)
	{
		_payload = Encoding.UTF8.GetBytes(payload);
		Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
	}

	protected override bool TryComputeLength(out long length)
	{
		length = 0;
		return false;
	}

	protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
	{
		return stream.WriteAsync(_payload.AsMemory()).AsTask();
	}

	protected override Task SerializeToStreamAsync(
		Stream stream,
		TransportContext? context,
		CancellationToken cancellationToken)
	{
		return stream.WriteAsync(_payload.AsMemory(), cancellationToken).AsTask();
	}
}
