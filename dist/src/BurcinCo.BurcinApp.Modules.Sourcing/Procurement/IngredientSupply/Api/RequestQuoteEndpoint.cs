using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Api;

internal static class RequestQuoteEndpoint
{
	internal static RouteGroupBuilder MapRequestQuote(this RouteGroupBuilder group)
	{
		group.MapPost("/", HandleAsync)
			.WithName($"Request{Constants.ServiceName}Quote")
			.Accepts<RequestQuoteRequest>("application/json")
			.Produces<IngredientQuoteView>(StatusCodes.Status202Accepted)
			.ProducesValidationProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status500InternalServerError);

		return group;
	}

	private static async Task<IResult> HandleAsync(
		[FromBody] RequestQuoteRequest request,
		[FromServices] IIngredientSupply service,
		CancellationToken cancellationToken)
	{
		try
		{
			var view = await service.RequestQuoteAsync(request, cancellationToken).ConfigureAwait(false);
			return Results.Accepted($"{Constants.RouteGroup}/{view.Id}", view);
		}
		catch (IngredientSupplyValidationException exception)
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["request"] = [.. exception.Errors],
			});
		}
	}
}
