using System.Threading;
using System.Threading.Tasks;

namespace BurcinCo.BurcinApp.Gateway.Contracts;

public interface IWebhookService
{
	Task<WebhookPublishResult> PublishAsync(string path, string body, CancellationToken cancellationToken = default);
}

public enum WebhookPublishOutcome
{
	Accepted,
	PayloadTooLarge,
	BrokerError,
}

public sealed record WebhookPublishResult(WebhookPublishOutcome Outcome, string? ErrorDetail = null);
