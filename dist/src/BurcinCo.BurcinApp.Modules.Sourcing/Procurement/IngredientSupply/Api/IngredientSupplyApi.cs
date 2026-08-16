using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Api;

internal static class IngredientSupplyApi
{
	internal static WebApplication MapIngredientSupplyApi(this WebApplication app)
	{
		app.MapGroup(Constants.RouteGroup)
			.WithTags(Constants.OpenApiTag)
			.WithOpenApi()
			.MapRequestQuote()
			.MapGetById();

		return app;
	}
}
