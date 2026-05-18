using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Responses;
using Microsoft.Extensions.Logging;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients;

/// <summary>
/// HTTP wrapper of <see cref="IRecipeService"/> targeting a separately-deployed Recipe module.
/// Recipe exposes its CRUD via OData controllers under <c>/odata/Recipe</c>; this client speaks
/// that surface and adapts between the cross-module DTOs (<see cref="RecipeCreateRequest"/>,
/// <see cref="RecipeView"/>) and the wire-shape Recipe entity that OData expects in payloads
/// and returns in responses.
///
/// OData URL form: <c>/odata/Recipe(123)</c> (parens around the key, not <c>/123</c>). Single-entity
/// GET responses are the entity object directly with extra <c>@odata.*</c> annotations that
/// System.Text.Json ignores by default.
/// Bound to <see cref="IRecipeService"/> when the <c>Modules.Recipe</c> feature flag is OFF.
/// </summary>
internal sealed partial class RecipeClient : IRecipeService
{
	private const string EntitySet = "/odata/Recipe";

	private readonly HttpClient _http;
	private readonly ILogger<RecipeClient> _logger;

	public RecipeClient(HttpClient http, ILogger<RecipeClient> logger)
	{
		ArgumentNullException.ThrowIfNull(http);
		ArgumentNullException.ThrowIfNull(logger);
		_http = http;
		_logger = logger;
	}

	public async Task<RecipeView?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
	{
		using var response = await _http.GetAsync($"{EntitySet}({id})", cancellationToken).ConfigureAwait(false);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		response.EnsureSuccessStatusCode();
		var wire = await response.Content.ReadFromJsonAsync<RecipeWire>(cancellationToken).ConfigureAwait(false);
		return wire is null ? null : ToView(wire);
	}

	public async Task<RecipeView> CreateAsync(RecipeCreateRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		// OData POST takes the entity-shaped body; the cross-module DTO maps onto the writable fields.
		using var response = await _http.PostAsJsonAsync(EntitySet, FromRequest(request), cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var wire = await response.Content.ReadFromJsonAsync<RecipeWire>(cancellationToken).ConfigureAwait(false);
		return wire is null ? throw new HttpRequestException("Recipe service returned no body on create.") : ToView(wire);
	}

	public async Task<RecipeView?> UpdateAsync(long id, RecipeCreateRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		using var response = await _http.PutAsJsonAsync($"{EntitySet}({id})", FromRequest(request), cancellationToken).ConfigureAwait(false);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		response.EnsureSuccessStatusCode();
		var wire = await response.Content.ReadFromJsonAsync<RecipeWire>(cancellationToken).ConfigureAwait(false);
		return wire is null ? null : ToView(wire);
	}

	public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
	{
		using var response = await _http.DeleteAsync($"{EntitySet}({id})", cancellationToken).ConfigureAwait(false);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
		response.EnsureSuccessStatusCode();
		return true;
	}

	private static RecipeWire FromRequest(RecipeCreateRequest r) =>
		new(0, r.ChefId, r.Name, r.Url, r.Yield, r.GramPerYield, r.CategoryCode);

	private static RecipeView ToView(RecipeWire w) =>
		new(w.Id, w.ChefId, w.Name, w.Url, w.Yield, w.GramPerYield, w.CategoryCode);

	/// <summary>
	/// Wire-format of the Recipe entity used over the OData boundary. We don't reference the EF entity
	/// type here because Modules.Nutrition shouldn't depend on Recipe's internal Models — only on the
	/// public Abstractions DTOs. <c>@odata.*</c> annotations on the response are ignored by default.
	/// </summary>
	private sealed record RecipeWire(
		[property: JsonPropertyName("Id")] long Id,
		[property: JsonPropertyName("ChefId")] long ChefId,
		[property: JsonPropertyName("Name")] string Name,
		[property: JsonPropertyName("Url")] string? Url,
		[property: JsonPropertyName("Yield")] int Yield,
		[property: JsonPropertyName("GramPerYield")] float GramPerYield,
		[property: JsonPropertyName("CategoryCode")] short? CategoryCode);
}
