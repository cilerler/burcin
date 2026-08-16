using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Host.Api;

internal static class MeEndpoint
{
	internal static IEndpointRouteBuilder MapMeEndpoint(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/me", GetCurrentUser)
			.WithName("GetCurrentUser")
			.WithTags("Host")
			.Produces<MeResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.RequireAuthorization();

		return endpoints;
	}

	private static IResult GetCurrentUser(ClaimsPrincipal user)
	{
		var subject = user.FindFirstValue("sub")
			?? user.FindFirstValue(ClaimTypes.NameIdentifier);
		var name = user.Identity?.Name
			?? user.FindFirstValue("name");

		return Results.Json(
			new MeResponse(subject, name),
			MeEndpointJsonSerializerContext.Default.MeResponse);
	}

	internal sealed record MeResponse(
		[property: JsonPropertyName("subject")] string? Subject,
		[property: JsonPropertyName("name")] string? Name);
}

[JsonSerializable(typeof(MeEndpoint.MeResponse))]
internal sealed partial class MeEndpointJsonSerializerContext : JsonSerializerContext;
