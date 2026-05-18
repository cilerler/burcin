using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Contracts;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Api;

/// <summary>
/// Minimal-API endpoints for recipe photos. Two independent concerns:
///   1. <c>GET /api/recipes/{id}/photo-url</c> — issue a signed URL the client can use to download.
///      Verifies the recipe exists; returns <see cref="SignedPhotoUrl"/>. This is the ONLY part the
///      client typically calls directly.
///   2. <c>GET /api/photos/{token}</c> — serve the photo bytes if the token validates. Token is
///      opaque to the client. This URL is what the client embeds in <c>&lt;img src&gt;</c>.
///
/// Why minimal API and not OData: this isn't entity CRUD. It's a verb-shaped flow ("issue a URL",
/// "serve bytes") that doesn't fit OData's entity-set model.
///
/// Why the stub returns a 1x1 transparent PNG: the demo's purpose is to prove the wire-up — content
/// negotiation, binary streaming, signed-URL pattern. Production implementations would stream from
/// blob storage (Azure Blob / S3 / etc.) using the recipe id resolved from the token.
/// </summary>
internal static class RecipePhotoApi
{
	// Embedded base64 of a 67-byte 1x1 transparent PNG. Decoded once at type init; same bytes
	// served on every download. Real impl would stream from blob storage per recipe.
	private static readonly byte[] PlaceholderPngBytes = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

	public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet(Constants.SignedUrlRoute, IssueSignedUrlAsync)
			.WithName($"Issue{Constants.ServiceName}SignedUrl")
			.WithTags(Constants.OpenApiTag)
			.Produces<SignedPhotoUrl>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound);

		var downloadGroup = endpoints.MapGroup(Constants.DownloadRouteGroup)
			.WithTags(Constants.OpenApiTag);

		downloadGroup.MapGet("/{token}", DownloadAsync)
			.WithName($"Download{Constants.ServiceName}")
			.Produces(StatusCodes.Status200OK, contentType: "image/png")
			.Produces(StatusCodes.Status404NotFound)
			.Produces(StatusCodes.Status410Gone);

		return endpoints;
	}

	private static async Task<IResult> IssueSignedUrlAsync(
		long recipeId,
		HttpContext httpContext,
		BurcinDatabaseDbContext db,
		IRecipePhotoService service,
		CancellationToken cancellationToken)
	{
		// Confirm the recipe exists before handing out a URL — otherwise a client could probe for
		// recipe ids by issuing URLs and seeing which ones return 404 vs 200 on download.
		var exists = await db.Recipes.AsNoTracking()
			.AnyAsync(r => r.Id == recipeId, cancellationToken)
			.ConfigureAwait(false);
		if (!exists) return Results.NotFound($"Recipe with Id={recipeId} not found.");

		var (token, expiresAt) = service.IssueToken(recipeId);
		// Build absolute URL using the request's base. PathBase + scheme + host gives the same
		// origin the caller used; ensures the URL works whether we're behind the gateway or hit directly.
		var request = httpContext.Request;
		var url = $"{request.Scheme}://{request.Host}{request.PathBase}{Constants.DownloadRouteGroup}/{token}";

		return Results.Ok(new SignedPhotoUrl(url, expiresAt));
	}

	private static IResult DownloadAsync(
		string token,
		IRecipePhotoService service,
		IMeterFactory meterFactory)
	{
		var recipeId = service.ValidateToken(token);
		if (recipeId is null) return Results.NotFound();

		// Bump the served counter only on the success path; rejected counter is bumped inside
		// the service so the rejection-reason tag stays attached.
		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		meter.CreateCounter<long>(Constants.Metrics.DownloadServed, unit: "{download}")
			.Add(1, new KeyValuePair<string, object?>(Constants.Tags.RecipeId, recipeId.Value));

		return Results.File(PlaceholderPngBytes, contentType: "image/png", fileDownloadName: $"recipe-{recipeId}.png");
	}
}
