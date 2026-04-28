using BurcinCo.BurcinApp.Gateway.Api;
using BurcinCo.BurcinApp.Gateway.Api.Filters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Gateway.Extensions;

internal static class EndpointExtensions
{
	public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapPost("/webhooks/{**path}", PostWebhookEndpoint.HandleAsync)
			.AddEndpointFilter<WebhookSecretAuthFilter>()
			.WithName("PostWebhook")
			.WithTags("Webhooks");

		return endpoints;
	}
}
