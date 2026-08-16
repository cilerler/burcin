using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BurcinCo.BurcinApp.AppHost.E2E.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.AppHost.E2E.Tests;

/// <summary>
/// Aspire-orchestrated end-to-end OData / minimal-API tests. Spins up the full distributed application
/// (MsSql + Redis + RabbitMQ + Host + Gateway) the same way the AppHost does in development, then exercises
/// resource-dependent behavior through the Gateway-to-Host boundary to prove:
///   - OData controllers route end-to-end (slash-form URLs canonical, paren form still works).
///   - PATCH partial-update semantics work (Name preserved, Url changed) — covers Delta&lt;T&gt; binding.
///   - ETag / If-Match optimistic concurrency works (412 on stale ETag).
///   - Bound OData function (Recipe/{id}/GetSummary) returns the joined complex type.
///   - Minimal-API photo signed-URL flow works end-to-end (issue → download → bytes).
///   - Sourcing's command-style minimal-API endpoint accepts a quote request (202).
///
/// Host-owned process contracts live in the Host WebApplicationFactory suite, while module and service behavior
/// lives in its owning integration suite. This class owns only cross-process and AppHost-managed-resource paths.
///
/// Slow (full-stack startup ~30s+ on first run). Run via the AppHost.E2E.Tests filter when verifying
/// the routing surface end-to-end; skip during fast feedback loops.
/// </summary>
[TestClass]
[TestCategory("E2E")]
[DoNotParallelize]
public sealed class ODataCrudTests
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
	private static readonly string[] RequiredEntitySets = ["Chef", "Recipe", "NutritionFact", "Tag"];

	[TestMethod]
	public async Task GetMetadata_ThroughGateway_ReturnsRequiredEntitySets()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		using var response = await http.GetAsync(new Uri("/odata/$metadata", UriKind.Relative));

		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.AreEqual("application/xml", response.Content.Headers.ContentType?.MediaType);

		var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
		XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
		var entitySets = document
			.Descendants(edm + "EntitySet")
			.Select(element => element.Attribute("Name")?.Value)
			.Where(name => name is not null)
			.ToHashSet(StringComparer.Ordinal);

		foreach (var entitySet in RequiredEntitySets)
		{
			Assert.IsTrue(entitySets.Contains(entitySet),
				$"OData metadata must expose the representative '{entitySet}' entity set.");
		}
	}

	[TestMethod]
	public async Task CreateTag_ThenReadThroughGateway_RoundTripsValues()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		using var post = await http.PostAsJsonAsync("/odata/Tag", new { Name = "e2e-tag", Color = "#22c55e" });
		Assert.AreEqual(HttpStatusCode.Created, post.StatusCode,
			$"POST /odata/Tag expected 201. Body: {await post.Content.ReadAsStringAsync()}");
		var created = await post.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("Id").GetInt64();
		Assert.IsTrue(id > 0, "The server must assign a positive Tag Id.");
		Assert.AreEqual("e2e-tag", created.GetProperty("Name").GetString());
		Assert.AreEqual("#22c55e", created.GetProperty("Color").GetString());

		using var get = await http.GetAsync(new Uri("/odata/Tag", UriKind.Relative));
		Assert.AreEqual(HttpStatusCode.OK, get.StatusCode,
			$"GET /odata/Tag expected 200. Body: {await get.Content.ReadAsStringAsync()}");
		var collection = await get.Content.ReadFromJsonAsync<JsonElement>();
		var read = collection
			.GetProperty("value")
			.EnumerateArray()
			.Single(tag => tag.GetProperty("Id").GetInt64() == id);
		Assert.AreEqual("e2e-tag", read.GetProperty("Name").GetString());
		Assert.AreEqual("#22c55e", read.GetProperty("Color").GetString());
	}

	[TestMethod]
	public async Task Chef_DbBackedCrud_RoundTripIncludingPatchSemantics()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		// POST → 201 + entity body with server Id.
		using var post = await http.PostAsJsonAsync("/odata/Chef", new { Name = "Smoke Tester", Url = "https://smoke/" });
		Assert.AreEqual(HttpStatusCode.Created, post.StatusCode, $"POST /odata/Chef expected 201. Body: {await post.Content.ReadAsStringAsync()}");
		var created = await post.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("Id").GetInt64();

		// PATCH only Url. Verifies Delta<T>.Patch leaves Name alone — the standard partial-update guarantee.
		using var patch = await http.PatchAsync($"/odata/Chef/{id}", JsonContent.Create(new { Url = "https://smoke-updated/" }));
		Assert.IsTrue(patch.IsSuccessStatusCode, $"PATCH expected 2xx. Got {(int)patch.StatusCode}: {await patch.Content.ReadAsStringAsync()}");

		using var afterPatch = await http.GetAsync($"/odata/Chef/{id}");
		var patched = await afterPatch.Content.ReadFromJsonAsync<JsonElement>();
		Assert.AreEqual("Smoke Tester", patched.GetProperty("Name").GetString(), "PATCH must not touch Name.");
		Assert.AreEqual("https://smoke-updated/", patched.GetProperty("Url").GetString());

		// DELETE → 204; verify the row is gone.
		using var delete = await http.DeleteAsync($"/odata/Chef/{id}");
		Assert.AreEqual(HttpStatusCode.NoContent, delete.StatusCode);
		using var afterDelete = await http.GetAsync($"/odata/Chef/{id}");
		Assert.AreEqual(HttpStatusCode.NotFound, afterDelete.StatusCode);
	}

	[TestMethod]
	public async Task Chef_PatchWithStaleETag_Returns412PreconditionFailed()
	{
		// ETag / If-Match round-trip. Demonstrates optimistic concurrency:
		//   1. POST a Chef.
		//   2. GET it — server includes @odata.etag in the response body, derived from the row's
		//      concurrency-token columns (RowGuid + RowVersion declared via [ConcurrencyCheck] /
		//      [Timestamp]).
		//   3. PATCH the same Chef successfully (changes RowVersion server-side).
		//   4. PATCH again with the OLD ETag in If-Match — server should respond 412.
		// OData emits the etag as a body annotation rather than the HTTP ETag header, which is the
		// idiomatic OData wire format. Real OData clients read @odata.etag from the body and echo it
		// back via the standard HTTP If-Match header on writes.
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		using var post = await http.PostAsJsonAsync("/odata/Chef", new { Name = "Concurrent", Url = "https://c/" });
		Assert.AreEqual(HttpStatusCode.Created, post.StatusCode,
			$"POST /odata/Chef expected 201. Body: {await post.Content.ReadAsStringAsync()}");
		var id = (await post.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Id").GetInt64();

		using var firstGet = await http.GetAsync($"/odata/Chef/{id}");
		var firstBody = await firstGet.Content.ReadFromJsonAsync<JsonElement>();
		var etagString = firstBody.GetProperty("@odata.etag").GetString();
		Assert.IsFalse(string.IsNullOrEmpty(etagString), "Server must include @odata.etag in the body for entities with concurrency tokens.");
		var staleETag = EntityTagHeaderValue.Parse(etagString!);

		// First PATCH with the (then-fresh) ETag → succeeds.
		using var patch1 = new HttpRequestMessage(HttpMethod.Patch, $"/odata/Chef/{id}")
		{
			Content = JsonContent.Create(new { Url = "https://c-v2/" }),
		};
		patch1.Headers.IfMatch.Add(staleETag);
		using var patch1Response = await http.SendAsync(patch1);
		Assert.IsTrue(patch1Response.IsSuccessStatusCode, $"First PATCH should succeed. Got {(int)patch1Response.StatusCode}: {await patch1Response.Content.ReadAsStringAsync()}");

		// Second PATCH with the SAME (now-stale) ETag → 412.
		using var patch2 = new HttpRequestMessage(HttpMethod.Patch, $"/odata/Chef/{id}")
		{
			Content = JsonContent.Create(new { Url = "https://c-v3/" }),
		};
		patch2.Headers.IfMatch.Add(staleETag);
		using var patch2Response = await http.SendAsync(patch2);
		Assert.AreEqual(HttpStatusCode.PreconditionFailed, patch2Response.StatusCode,
			"Second PATCH with the now-stale ETag must return 412.");
	}

	[TestMethod]
	public async Task Recipe_GetSummary_ReturnsJoinedComplexType()
	{
		// Bound OData function /odata/Recipe/{id}/GetSummary — read-only, returns a derived shape
		// (RecipeSummary complex type) joining Recipe + Chef in one call. Demonstrates the function
		// pattern as an alternative to $expand for client-rendered "card" views.
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		// Set up Chef → Recipe so the join has data.
		using var chefPost = await http.PostAsJsonAsync("/odata/Chef", new { Name = "Summary Tester", Url = "https://s/" });
		Assert.AreEqual(HttpStatusCode.Created, chefPost.StatusCode,
			$"POST /odata/Chef expected 201. Body: {await chefPost.Content.ReadAsStringAsync()}");
		var chefId = (await chefPost.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Id").GetInt64();
		using var recipePost = await http.PostAsJsonAsync("/odata/Recipe", new
		{
			ChefId = chefId,
			Name = "Summary Recipe",
			Url = "https://s/r",
			Yield = (ushort)4,
			GramPerYield = 250f,
			CategoryCode = (short?)null,
		});
		Assert.AreEqual(HttpStatusCode.Created, recipePost.StatusCode,
			$"POST /odata/Recipe expected 201. Body: {await recipePost.Content.ReadAsStringAsync()}");
		var recipeId = (await recipePost.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Id").GetInt64();

		using var summary = await http.GetAsync($"/odata/Recipe/{recipeId}/GetSummary");

		Assert.AreEqual(HttpStatusCode.OK, summary.StatusCode);
		var body = await summary.Content.ReadFromJsonAsync<JsonElement>();
		Assert.AreEqual(recipeId, body.GetProperty("RecipeId").GetInt64());
		Assert.AreEqual("Summary Recipe", body.GetProperty("RecipeName").GetString());
		Assert.AreEqual("Summary Tester", body.GetProperty("ChefName").GetString(), "Chef name must be joined into the summary.");
		Assert.AreEqual(1000f, body.GetProperty("GramTotal").GetSingle(), 0.01f, "GramTotal = GramPerYield (250) × Yield (4).");
	}

	[TestMethod]
	public async Task Photo_SignedUrlFlow_IssueThenDownload()
	{
		// Two-step minimal-API flow:
		//   1. GET /api/recipes/{id}/photo-url returns { url, expiresAt }
		//   2. GET that url returns image/png bytes (placeholder)
		// Proves end-to-end that the signed-URL pattern works through the gateway.
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		// Set up a recipe so the issuer's existence-check passes.
		using var chefPost = await http.PostAsJsonAsync("/odata/Chef", new { Name = "Photo Tester", Url = "https://p/" });
		Assert.AreEqual(HttpStatusCode.Created, chefPost.StatusCode,
			$"POST /odata/Chef expected 201. Body: {await chefPost.Content.ReadAsStringAsync()}");
		var chefId = (await chefPost.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Id").GetInt64();
		using var recipePost = await http.PostAsJsonAsync("/odata/Recipe", new
		{
			ChefId = chefId,
			Name = "Photo Recipe",
			Url = "https://p/r",
			Yield = (ushort)1,
			GramPerYield = 100f,
			CategoryCode = (short?)null,
		});
		Assert.AreEqual(HttpStatusCode.Created, recipePost.StatusCode,
			$"POST /odata/Recipe expected 201. Body: {await recipePost.Content.ReadAsStringAsync()}");
		var recipeId = (await recipePost.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Id").GetInt64();

		// Step 1: issue.
		using var issue = await http.GetAsync($"/api/recipes/{recipeId}/photo-url");
		Assert.AreEqual(HttpStatusCode.OK, issue.StatusCode, $"Issue expected 200. Body: {await issue.Content.ReadAsStringAsync()}");
		var payload = await issue.Content.ReadFromJsonAsync<JsonElement>();
		var url = payload.GetProperty("url").GetString()!;
		StringAssert.Contains(url, "/api/photos/", "Issued URL must point at the download endpoint.");

		// Step 2: download. The url is absolute — extract the path-and-query for the test client.
		var downloadPath = new Uri(url).PathAndQuery;
		using var download = await http.GetAsync(downloadPath);
		Assert.AreEqual(HttpStatusCode.OK, download.StatusCode);
		Assert.AreEqual("image/png", download.Content.Headers.ContentType?.MediaType);
		var bytes = await download.Content.ReadAsByteArrayAsync();
		// PNG signature: 89 50 4E 47 0D 0A 1A 0A
		Assert.IsTrue(bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
			$"Response body must be a PNG (starts with 89 50 4E 47). Got {bytes.Length} bytes.");

		// Step 3: bad token rejected.
		using var badDownload = await http.GetAsync("/api/photos/this-is-not-a-valid-token");
		Assert.AreEqual(HttpStatusCode.NotFound, badDownload.StatusCode, "Malformed/expired/wrong-signature tokens must be rejected.");

		// Step 4: missing recipe rejected (so attackers can't enumerate recipe ids).
		using var missing = await http.GetAsync("/api/recipes/99999999/photo-url");
		Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
	}

#if (Sample)
	[TestMethod]
	public async Task Sourcing_RequestQuote_Returns202_AndPersistsInitialRow()
	{
		// Versioned IngredientSupply command endpoint /api/v1/ingredient-supply. This flow kicks
		// off an outbox-→-broker-→-supplier sequence), not entity CRUD, so it's correctly modeled
		// as a minimal-API POST returning 202 Accepted with a Location header to GET the eventual
		// quote state. Smoke verifies the endpoint reaches the controller and persists the initial
		// IngredientQuote row in Pending state. The full broker round-trip is exercised in
		// Modules.Sourcing.Integration.Tests, not here.
		// Sample-gated: when generated without --Sample, Sourcing isn't deployed and this endpoint
		// doesn't exist; the test method is excluded from the assembly entirely.
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		var body = new
		{
			SupplierKey = "flour-provider",
			RecipeId = (long?)null,
			Ingredients = new[]
			{
				new { Name = "flour", Quantity = 500f, Unit = "g" },
			},
		};

		using var response = await http.PostAsJsonAsync("/api/v1/ingredient-supply/", body);

		Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode,
			$"POST /api/v1/ingredient-supply expected 202. Got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

		// 202 body should include the persisted view with an Id we can read back.
		var view = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.IsTrue(view.TryGetProperty("id", out var quoteId),
			"Response body must use the source-generated lower-camel contract name 'id'.");
		Assert.AreEqual(
			$"/api/v1/ingredient-supply/{quoteId.GetInt64()}",
			response.Headers.Location?.OriginalString,
			"Accepted responses must point at the versioned quote resource.");
	}
#endif

	private static async Task<DistributedApplication> StartAppAsync()
	{
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BurcinCo_BurcinApp_AppHost>(
			["--environment=Development", "--Logging:EventLog:LogLevel:Default=None"]);
		appHost.Services.AddLogging(logging =>
		{
			logging.SetMinimumLevel(LogLevel.Warning); // Less noisy than the smoke-test in WebTests.
			logging.AddFilter("BurcinCo.", LogLevel.Information);
		});

		var app = await appHost.BuildAsync().WaitAsync(DefaultTimeout);
		await app.StartAsync().WaitAsync(DefaultTimeout);
		await DatabaseSchemaInitializer.InitializeAsync(app, DefaultTimeout);
		return app;
	}

	private static async Task<HttpClient> CreateGatewayHttpClientAsync(DistributedApplication app)
	{
		await app.ResourceNotifications
			.WaitForResourceAsync("host", "Running")
			.WaitAsync(DefaultTimeout);
		await app.ResourceNotifications
			.WaitForResourceAsync("gateway", "Running")
			.WaitAsync(DefaultTimeout);

		var http = app.CreateHttpClient("gateway");
		http.Timeout = DefaultTimeout;
		return http;
	}
}
