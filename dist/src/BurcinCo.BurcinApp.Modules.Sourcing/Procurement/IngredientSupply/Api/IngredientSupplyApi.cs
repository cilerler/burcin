using System.Threading;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

internal static class IngredientSupplyApi
{
	public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup(Constants.RouteGroup)
			.WithTags(Constants.OpenApiTag);

		group.MapPost("/", RequestQuoteAsync)
			.WithName($"Request{Constants.ServiceName}Quote")
			.Produces<IngredientQuoteView>(StatusCodes.Status202Accepted);

		group.MapGet("/{id:long}", GetByIdAsync)
			.WithName($"Get{Constants.ServiceName}QuoteById")
			.Produces<IngredientQuoteView>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound);

		return endpoints;
	}

	private static async System.Threading.Tasks.Task<IResult> RequestQuoteAsync(
		RequestQuoteRequest request, IIngredientSupplyService service, CancellationToken cancellationToken)
	{
		var view = await service.RequestQuoteAsync(request, cancellationToken).ConfigureAwait(false);
		return Results.Accepted($"{Constants.RouteGroup}/{view.Id}", view);
	}

	private static async System.Threading.Tasks.Task<IResult> GetByIdAsync(
		long id, IIngredientSupplyService service, CancellationToken cancellationToken)
	{
		var view = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
		return view is null ? Results.NotFound() : Results.Ok(view);
	}
}
