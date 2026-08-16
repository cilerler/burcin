using System.Collections.Generic;
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
		IWebhook webhook,
		CancellationToken cancellationToken)
	{
		var result = await webhook.PublishAsync(
			path,
			context.Request.Body,
			context.Request.ContentLength,
			cancellationToken).ConfigureAwait(false);

		return result.Outcome switch
		{
			WebhookPublishOutcome.Accepted => Results.Accepted(),
			WebhookPublishOutcome.InvalidPayload => Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["body"] = [result.ErrorDetail ?? "The request body must contain valid JSON."],
			}),
			WebhookPublishOutcome.PayloadTooLarge => Results.Problem(
				statusCode: StatusCodes.Status413PayloadTooLarge,
				title: "Webhook payload too large."),
			WebhookPublishOutcome.BrokerError => Results.Problem(
				statusCode: StatusCodes.Status502BadGateway,
				title: "Webhook delivery failed."),
			_ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
		};
	}
}
