using BurcinCo.BurcinApp.Gateway.Webhook.Api.Filters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Api;

/// <summary>
/// Entry point for the Webhook service's HTTP surface. Per the dotnet-service-generator skill,
/// each service's <c>Api/{ServiceName}Api.cs</c> owns the route grouping and filter wiring; the
/// individual <c>{Verb}Endpoint.cs</c> files hold each operation's handler logic. <c>MapWebhook()</c>
/// is what the Gateway-level <c>ProgramExtensionsCustom.ConfigureCustomPipeline</c> calls.
/// </summary>
internal static class WebhookApi
{
	public static IEndpointRouteBuilder MapWebhook(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapPost("/webhooks/{**path}", PostEndpoint.HandleAsync)
			.AddEndpointFilter<WebhookSecretAuthFilter>()
			.WithName("PostWebhook")
			.WithTags("Webhooks");

		return endpoints;
	}
}
