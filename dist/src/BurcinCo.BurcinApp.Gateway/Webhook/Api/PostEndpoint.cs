using System.IO;
using System.Threading;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Webhook.Contracts;

using Microsoft.AspNetCore.Http;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Api;

internal static class PostEndpoint
{
	public static async Task<IResult> HandleAsync(
		HttpContext context,
		string path,
		IWebhookService webhookService,
		CancellationToken cancellationToken)
	{
		using var reader = new StreamReader(context.Request.Body);
		var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

		var result = await webhookService.PublishAsync(path, body, cancellationToken).ConfigureAwait(false);

		return result.Outcome switch
		{
			WebhookPublishOutcome.Accepted => Results.Accepted(),
			WebhookPublishOutcome.PayloadTooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
			WebhookPublishOutcome.BrokerError => Results.StatusCode(StatusCodes.Status502BadGateway),
			_ => Results.StatusCode(StatusCodes.Status500InternalServerError),
		};
	}
}
