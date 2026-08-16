using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Contracts;

internal interface IWebhook
{
	Task<WebhookPublishResult> PublishAsync(
		string path,
		Stream body,
		long? contentLength,
		CancellationToken cancellationToken);
}

internal enum WebhookPublishOutcome
{
	Accepted,
	InvalidPayload,
	PayloadTooLarge,
	BrokerError,
}

internal sealed record WebhookPublishResult(WebhookPublishOutcome Outcome, string? ErrorDetail = null);
