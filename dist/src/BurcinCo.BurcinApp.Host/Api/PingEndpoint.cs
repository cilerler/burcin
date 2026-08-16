using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Host.Api;

internal static class PingEndpoint
{
	internal static IEndpointRouteBuilder MapPingEndpoint(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/ping", static () => Results.Text("pong", contentType: "text/plain"))
			.WithName("Ping")
			.WithTags("Host")
			.Produces(StatusCodes.Status200OK, contentType: "text/plain")
			.AllowAnonymous();

		return endpoints;
	}
}
