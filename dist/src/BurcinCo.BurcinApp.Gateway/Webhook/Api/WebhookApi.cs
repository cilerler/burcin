using BurcinCo.BurcinApp.Gateway.Webhook.Api.Filters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using NetworkSecurityConstants = BurcinCo.BurcinApp.Gateway.NetworkSecurity.Constants;
using RateLimitingConstants = BurcinCo.BurcinApp.Gateway.RateLimiting.Constants;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Api;

/// <summary>
/// Low-level HTTP adapter for the Gateway-owned Webhook edge capability. Only its
/// <c>StartupExtensions.MapWebhook</c> wrapper invokes it.
/// </summary>
internal static class WebhookApi
{
	internal static WebApplication MapWebhookApi(this WebApplication app)
	{
		app.MapPost(Constants.RoutePattern, PostEndpoint.HandleAsync)
			.AddEndpointFilter<WebhookSecretAuthFilter>()
			.RequireAuthorization(NetworkSecurityConstants.IpSafelistPolicies.Webhook)
			.RequireRateLimiting(RateLimitingConstants.Policies.Webhook)
			.WithName("PostWebhook")
			.WithTags(Constants.OpenApiTag)
			.WithOpenApi()
			.Produces(StatusCodes.Status202Accepted)
			.ProducesValidationProblem(StatusCodes.Status400BadRequest)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status413PayloadTooLarge)
			.ProducesProblem(StatusCodes.Status429TooManyRequests)
			.ProducesProblem(StatusCodes.Status502BadGateway)
			.ProducesProblem(StatusCodes.Status500InternalServerError);

		return app;
	}
}
