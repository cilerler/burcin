using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
#if (EntityFrameworkScaffold)
using BurcinCo.BurcinApp.Data;
using Microsoft.EntityFrameworkCore;
using Ruya.EntityFrameworkCore.SqlServer;
using Ruya.EntityFrameworkCore.SqlServer.BatchLock;
#if (Sample)
using BurcinCo.BurcinApp.Modules.Nutrition.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Extensions;
using BurcinCo.BurcinApp.Modules.Sourcing.Extensions;
using Ruya.Extensions.Configuration;
using Ruya.Services.MessageQueue.Extensions;
using Ruya.Services.MessageQueue.RabbitMq;
using Ruya.Services.ReliableMessaging.Extensions;
using Ruya.Services.ReliableMessaging.MessageQueue.Extensions;
#endif
#endif
#if (ODataServices)
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Batch;
using Microsoft.OData.ModelBuilder;
#endif

namespace BurcinCo.BurcinApp.Host;

/// <summary>
/// Custom (project-specific) registration: shared DbContext + per-module activation gated by feature flags.
/// The same Docker image powers all module deployments; only the FeatureManagement section in each
/// Deployment's appsettings differs to choose which module is active.
///
/// IMPORTANT: This template's modules (Recipe, Nutrition) expose entity CRUD exclusively through OData
/// controllers. If you generate the template with <c>--EntityFramework</c> but without <c>--OData</c>,
/// those modules' DI will register but their endpoints won't be routable. Only Modules.Sourcing exposes
/// minimal-API endpoints (because RequestQuote is a command, not entity CRUD). Recommendation: always
/// generate with both switches together.
/// </summary>
internal static class ProgramExtensionsCustom
{
#if (Sample)
	// Section name comes from Ruya.Primitives.FeatureFlags.ConfigurationSectionName — same constant
	// ProgramExtensions.cs uses for AddFeatureManagement, so there's one source of truth for the string.
	// Module-flag names are local because they ARE this template's contract with deployment overlays
	// (every Kustomize/Helm overlay flips these specific keys); a Ruya-side constant would couple our
	// deployment shape to library naming.
	private const string RecipeModuleFlag = "Modules.Recipe";
	private const string NutritionModuleFlag = "Modules.Nutrition";
	private const string SourcingModuleFlag = "Modules.Sourcing";
#endif

	public static IHostApplicationBuilder AddCustomServices(this IHostApplicationBuilder builder)
	{
		// Wire app-owned ActivitySources into the OpenTelemetry tracer. Each module-component-service
		// declares an `ActivitySourceName` in its Constants.Activities, scoped under the
		// `BurcinCo.BurcinApp.*` prefix. Wildcards in OTel cover them all in one line so adding new
		// modules doesn't require a tracer-side update.
		builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource("BurcinCo.BurcinApp.*"));

#if (EntityFrameworkScaffold)
		// Single shared BurcinDatabaseDbContext registration via the BurcinCo.BurcinApp.Data project.
		// All modules read/write through this same context. Per-deployment SQL user permissions
		// (broad SELECT, narrow INSERT/UPDATE/DELETE on the module's own schema) enforce module-only
		// writes at the database level — see the modular-polylith ADR.
		builder.Services.AddBurcinDatabaseDbContext();
		builder.Services.AddBatchLockOperations<BurcinDatabaseDbContext>();
		builder.Services.AddBulkInsertOperations<BurcinDatabaseDbContext>();

#if (Sample)
		// Module activation. Each demo module's StartupExtensions runs only when its master feature
		// flag is enabled in this deployment's config. Single image, multiple deployments.
		// Reference modules are Sample-gated — Sample=off produces a bring-your-own-modules skeleton.
		var fm = builder.Configuration.GetSection(FeatureFlags.ConfigurationSectionName);
		var recipeEnabled = fm.GetValue<bool>(RecipeModuleFlag);
		var nutritionEnabled = fm.GetValue<bool>(NutritionModuleFlag);
		var sourcingEnabled = fm.GetValue<bool>(SourcingModuleFlag);

		if (recipeEnabled)
		{
			builder.Services.AddRecipeModule(builder.Configuration);
		}
		if (nutritionEnabled)
		{
			builder.Services.AddNutritionModule(builder.Configuration);
		}
		if (sourcingEnabled)
		{
			// Reliable-messaging composition root. AddReliableMessaging() is called once at app level
			// (Polly throws on duplicate ResiliencePipeline keys, so we don't repeat it inside Data).
			// AddBurcinDatabaseReliableMessaging() chains the per-context outbox/inbox + EF stores +
			// interceptor configurer onto the shared BurcinDatabaseDbContext (Data project owns the
			// schema and runtime wiring). AddMessageQueueOutboundDispatcher() drains the Outbox to
			// RabbitMQ; the QuoteRequestDispatcher worker subscribes to that topic and makes the
			// actual external HTTP call to the configured supplier.
			builder.Services.AddMessageQueue(builder.Configuration)
				.AddRabbitMQ(builder.Configuration);
			builder.Services.AddReliableMessaging()
				.AddBurcinDatabaseReliableMessaging()
				.AddMessageQueueOutboundDispatcher();

			builder.Services.AddSourcingModule(builder.Configuration);
		}
#endif
#endif

#if (ODataServices)
		// OData services. The EDM is split in two:
		//   1. Data's central AddBurcinDatabaseEntitySets() — every DB-backed entity, registered
		//      unconditionally. The polylith allows cross-module reads (every module sees the whole
		//      DbContext), so the read-surface declaration follows the DbContext, not the active-controller
		//      set. $expand across module boundaries works in every deployment that hosts the entity's
		//      owning controller, because OData runs the expansion server-side against the shared
		//      DbContext. Direct GET /odata/{EntitySet} still 404s when the module's controller isn't
		//      mounted — that's controller-activation, separate from EDM advertisement.
		//   2. Per-module module-private contributions (non-DB entity sets like Tag, bound functions
		//      like Recipe.GetSummary) — wired only when the owning module is feature-flag-active.
		// Controllers (ChefController, RecipeController, etc.) are auto-discovered by MapControllers below;
		// they only become reachable when their module's services are registered, since controller
		// construction depends on the module's services. AddControllers() is required by the OData
		// package even with minimal-API endpoints elsewhere (it provides routing infrastructure).
		var edmBuilder = new ODataConventionModelBuilder();
#if (EntityFrameworkScaffold)
		edmBuilder.AddBurcinDatabaseEntitySets();
#if (Sample)
		if (recipeEnabled) edmBuilder.AddRecipeModuleEdmContributions();
		// Modules.Nutrition has no non-DB entities or bound functions, so it contributes nothing beyond
		// what Data registers centrally (NutritionFact is part of the central set).
#endif
#endif

		builder.Services.AddControllers()
			.AddOData(options =>
			{
				options.Select().Expand().Filter().OrderBy().Count().SkipToken().SetMaxTop(100);
				// Batch handler enables POST /odata/$batch — clients can pack multiple operations
				// into one HTTP round-trip. Each sub-request goes through the same routing/DI/middleware
				// as a standalone call, so existing OData controllers don't need any changes to participate.
				// Default handler supports both JSON and multipart/mixed batch formats.
				options.AddRouteComponents("odata", edmBuilder.GetEdmModel(), new DefaultODataBatchHandler());
			});
#endif

		return builder;
	}

	public static WebApplication ConfigureCustomPipeline(this WebApplication app)
	{
#if (ODataServices)
		// OData batch middleware. MUST be registered before UseRouting (which the WebApplication's
		// auto-middleware adds when controllers are mapped). Order matters here — without this call,
		// /odata/$batch returns 404 because no middleware claims the path.
		app.UseODataBatching();

		// MapControllers routes both OData controllers (under /odata via AddRouteComponents) and any
		// classic MVC controllers. The Recipe and Nutrition modules expose entity CRUD this way.
		app.MapControllers();
		app.MapDefaultControllerRoute();
#endif

#if (Sample)
		// Modules with minimal-API endpoints need explicit Map* calls. OData controllers in those
		// modules are picked up by app.MapControllers() above and DON'T need a Map* call.
		// - Recipe: minimal-API photo signed-URL + download stub (Catalog/RecipePhoto)
		// - Sourcing: minimal-API command endpoints (RequestQuote, GetById)
		// - Nutrition: no minimal-API endpoints; entirely OData
		var fm = app.Configuration.GetSection(FeatureFlags.ConfigurationSectionName);
		if (fm.GetValue<bool>(RecipeModuleFlag))
		{
			app.MapRecipeModule();
		}
		if (fm.GetValue<bool>(SourcingModuleFlag))
		{
			app.MapSourcingModule();
		}
#endif

		return app;
	}
}
