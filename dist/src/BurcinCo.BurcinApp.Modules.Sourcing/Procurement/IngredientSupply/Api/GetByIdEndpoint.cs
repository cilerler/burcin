using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Api;

internal static class GetByIdEndpoint
{
	internal static RouteGroupBuilder MapGetById(this RouteGroupBuilder group)
	{
		group.MapGet("/{id:long}", HandleAsync)
			.WithName($"Get{Constants.ServiceName}QuoteById")
			.Produces<IngredientQuoteView>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError);

		return group;
	}

	private static async Task<IResult> HandleAsync(
		long id,
		[FromServices] IIngredientSupply service,
		CancellationToken cancellationToken)
	{
		var view = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
		return view is null ? Results.NotFound() : Results.Ok(view);
	}
}
