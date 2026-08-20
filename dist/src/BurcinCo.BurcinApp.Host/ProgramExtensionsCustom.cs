using System;
using BurcinCo.BurcinApp.Host.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
#if (EntityFrameworkScaffold)
using BurcinCo.BurcinApp.Data;
using Ruya.EntityFrameworkCore.SqlServer;
using Ruya.EntityFrameworkCore.SqlServer.BatchLock;
#endif
#if (Sample)
using BurcinCo.BurcinApp.Modules.Nutrition.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Extensions;
using BurcinCo.BurcinApp.Modules.Sourcing.Extensions;
#if (ODataServices)
using NutritionModuleStartupExtensions = BurcinCo.BurcinApp.Modules.Nutrition.Extensions.StartupExtensions;
using RecipeModuleStartupExtensions = BurcinCo.BurcinApp.Modules.Recipe.Extensions.StartupExtensions;
#endif
using Ruya.Services.MessageQueue.Extensions;
using Ruya.Services.MessageQueue.RabbitMq;
using Ruya.Services.ReliableMessaging.Extensions;
using Ruya.Services.ReliableMessaging.MessageQueue.Extensions;
#endif
#if (ODataServices)
using Microsoft.AspNetCore.Mvc.ApplicationParts;
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
	public static IHostApplicationBuilder AddCustomServices(
		this IHostApplicationBuilder builder,
		CapabilitySelection capabilities)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(capabilities);

		// Registration and endpoint mapping resolve this exact immutable snapshot instance.
		builder.Services.AddSingleton(capabilities);

		// Wire app-owned ActivitySources into the OpenTelemetry tracer. Each module-component-service
		// declares an `ActivitySourceName` in its Constants.Activities, scoped under the
		// `BurcinCo.BurcinApp.*` prefix. Wildcards in OTel cover them all in one line so adding new
		// modules doesn't require a tracer-side update.
		builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(
			$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.*"));

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
		if (capabilities.Recipe)
		{
			builder.Services.AddRecipeModule();
		}
		if (capabilities.Nutrition)
		{
			builder.Services.AddNutritionModule(recipeIsLocal: capabilities.Recipe);
		}
		if (capabilities.Sourcing)
		{
			// Reliable-messaging composition root. AddReliableMessaging() is called once at app level
			// (Polly throws on duplicate ResiliencePipeline keys, so we don't repeat it inside Data).
			// AddBurcinDatabaseReliableMessaging() chains the per-context outbox/inbox + EF stores +
			// interceptor configurer onto the shared BurcinDatabaseDbContext (Data project owns the
			// schema and runtime wiring). AddMessageQueueOutboundDispatcher() drains the Outbox to
			// RabbitMQ; the IngredientQuoteRequestedEventSubscriber subscribes to that topic and delegates the
			// actual external HTTP call to the configured supplier.
			builder.Services.AddMessageQueue()
				.AddSourcingMessageContracts()
				.AddRabbitMQ();
			builder.Services.AddReliableMessaging()
				.AddBurcinDatabaseReliableMessaging()
				.AddMessageQueueOutboundDispatcher();

			builder.Services.AddSourcingModule();
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
		// AddControllers() is required by the OData package even with minimal-API endpoints elsewhere.
		// Disabled module assemblies are removed from MVC application parts below, before the provider
		// is built, so their controller routes do not enter the endpoint table at all.
		var edmBuilder = new ODataConventionModelBuilder();
#if (EntityFrameworkScaffold)
		edmBuilder.AddBurcinDatabaseEntitySets();
#if (Sample)
		if (capabilities.Recipe) edmBuilder.AddRecipeModuleEdmContributions();
		// Modules.Nutrition has no non-DB entities or bound functions, so it contributes nothing beyond
		// what Data registers centrally (NutritionFact is part of the central set).
#endif
#endif

		var mvcBuilder = builder.Services.AddControllers();
#if (Sample)
		mvcBuilder.ConfigureApplicationPartManager(
			parts => RemoveDisabledModuleControllerParts(parts, capabilities));
#endif
		mvcBuilder.AddOData(options =>
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

	/// <summary>
	/// The OData surface, wired BEFORE the default pipeline (called from Program.cs ahead of
	/// ConfigureDefaultPipeline). Only UseODataBatching has a hard ordering constraint — it must
	/// precede UseRouting, or routing matches an endpoint first and every /odata/$batch
	/// sub-request 404s; it passes through for any other path. The Map* calls are
	/// position-independent (endpoint registrations are collected into the route builder's data
	/// sources and consumed by UseRouting wherever they were declared) — they live here so the
	/// whole OData wiring reads in one place.
	/// </summary>
	public static WebApplication ConfigureCustomEarlyPipeline(this WebApplication app)
	{
#if (ODataServices)
		// OData batch middleware. MUST be registered before UseRouting (which the WebApplication's
		// auto-middleware adds when controllers are mapped). Order matters here — without this call,
		// /odata/$batch returns 404 because no middleware claims the path.
		app.UseODataBatching();

		// MapControllers routes both OData controllers (under /odata via AddRouteComponents) and any
		// classic MVC controllers.
		app.MapControllers();
		app.MapDefaultControllerRoute();

#endif
		return app;
	}

	public static WebApplication ConfigureCustomPipeline(this WebApplication app)
	{

#if (Sample)
		// Modules with minimal-API endpoints need explicit Map* calls. OData controllers in those
		// modules are picked up by app.MapControllers() above and DON'T need a Map* call.
		// - Recipe: minimal-API photo signed-URL + download stub (Catalog/RecipePhoto)
		// - Sourcing: minimal-API command endpoints (RequestQuote, GetById)
		// - Nutrition: no minimal-API endpoints; entirely OData
		var capabilities = app.Services.GetRequiredService<CapabilitySelection>();
		if (capabilities.Recipe)
		{
			app.MapRecipeModule(capabilities.Recipe);
		}

		if (capabilities.Sourcing)
		{
			app.MapSourcingModule(capabilities.Sourcing);
		}
#endif

		return app;
	}

#if (ODataServices)
#if (Sample)
	private static void RemoveDisabledModuleControllerParts(
		ApplicationPartManager parts,
		CapabilitySelection capabilities)
	{
		ArgumentNullException.ThrowIfNull(parts);
		ArgumentNullException.ThrowIfNull(capabilities);

		if (!capabilities.Recipe)
		{
			RemoveAssemblyPart(parts, typeof(RecipeModuleStartupExtensions).Assembly);
		}

		if (!capabilities.Nutrition)
		{
			RemoveAssemblyPart(parts, typeof(NutritionModuleStartupExtensions).Assembly);
		}
	}

	private static void RemoveAssemblyPart(ApplicationPartManager parts, System.Reflection.Assembly assembly)
	{
		for (var index = parts.ApplicationParts.Count - 1; index >= 0; index--)
		{
			if (parts.ApplicationParts[index] is AssemblyPart assemblyPart &&
				assemblyPart.Assembly == assembly)
			{
				parts.ApplicationParts.RemoveAt(index);
			}
		}
	}
#endif
#endif
}
