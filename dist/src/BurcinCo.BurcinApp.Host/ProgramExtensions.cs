using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Host.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Ruya.AspNetCore.Middleware.AppEnvironmentResponseHeaders;
using Ruya.AspNetCore.Diagnostics.GlobalExceptionHandler;
using Ruya.Diagnostics.DistributedTracing;
using Ruya.Extensions.Configuration;
using Ruya.OpenTelemetry;
using Scalar.AspNetCore;
#if (CacheRedis)
using Ruya.Services.DistributedLock.Redis.Extensions;
using StackExchange.Redis;
#endif

namespace BurcinCo.BurcinApp.Host;

internal static class ProgramExtensions
{
	//! do not change the order of these calls without understanding their dependencies.

	public static IHostApplicationBuilder AddDefaultServices(this IHostApplicationBuilder builder)
	{
		builder.Configuration.AddKubernetesConfiguration();
		builder.Configuration.AddEnvironmentVariablesWithPrefix();

		builder.Services.AddGlobalExceptionHandlerService();

		builder.Services.AddControllers()
			.AddJsonOptions(options =>
			{
				options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
				options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			});

		builder.Services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer();
		builder.Services
			.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
			.BindConfiguration("Authentication:Schemes:Bearer")
			.Configure(options => options.MapInboundClaims = false)
			.Validate(
				options => !string.IsNullOrWhiteSpace(options.Authority) ||
					!string.IsNullOrWhiteSpace(options.MetadataAddress),
				"Bearer authentication requires Authority or MetadataAddress deployment configuration.")
			.Validate(
				options => !string.IsNullOrWhiteSpace(options.Audience),
				"Bearer authentication requires Audience deployment configuration.")
			.ValidateOnStart();
		builder.Services.AddAuthorization();

		builder.Services.AddMemoryCache();
#if (!CacheExists)
		builder.Services.AddDistributedMemoryCache();
#endif

#if (CacheSqlServer)
		builder.Services.AddDistributedSqlServerCache(options =>
		{
			options.ConnectionString = builder.Configuration.GetConnectionString(builder.Configuration["DistributedCache:SqlServer:ConnectionStringKey"]);
			options.SchemaName = builder.Configuration["DistributedCache:SqlServer:SchemaName"];
			options.TableName = builder.Configuration["DistributedCache:SqlServer:TableName"];
		});
#endif

#if (CacheRedis)
		builder.Services.AddStackExchangeRedisCache(options =>
		{
			options.Configuration = builder.Configuration.GetConnectionString(builder.Configuration["DistributedCache:Redis:ConnectionStringKey"]);
			options.InstanceName = builder.Configuration["DistributedCache:Redis:InstanceName"];
			options.ConfigurationOptions = ConfigurationOptions.Parse(options.Configuration);
			options.ConfigurationOptions.AbortOnConnectFail = true;
		});
		builder.Services.AddRedisDistributedLock();
#endif

		builder.Services.AddHybridCache();

		builder.Services.AddSingleton(TimeProvider.System);

		builder.Services.AddHttpContextAccessor();
		builder.Services.AddServiceDiscovery();

		builder.Services.AddHttpClient();
		builder.Services.ConfigureHttpClientDefaults(http =>
		{
			http.AddServiceDiscovery();
			//http.AddStandardResilienceHandler();
		});

		builder.ConfigureOpenTelemetry();
		builder.Services.AddDistributedTracingService();

		builder.Services.AddResponseCaching();
		builder.Services.AddResponseCompression();

		builder.Services.AddFeatureManagement(builder.Configuration.GetSection(FeatureFlags.ConfigurationSectionName))
			//.AddFeatureFilter<TargetingFilter>()
			.AddFeatureFilter<PercentageFilter>()
			.AddFeatureFilter<TimeWindowFilter>();

		builder.Services.AddAppEnvironmentResponseHeaders();

		builder.Services.AddOpenApi();

		builder.Services.AddHostedService<StartupBackgroundService>();
		builder.Services.AddSingleton<StartupHealthCheck>();
		builder.Services.AddResourceMonitoring();
		builder.Services.AddHealthChecks()
						.AddResourceUtilizationHealthCheck()
						.AddApplicationLifecycleHealthCheck()
						.AddCheck<StartupHealthCheck>("Startup", tags: ["startup", "ready"])
#if (EntityFrameworkScaffold)
			.AddSqlServer(
				connectionString: builder.Configuration["ConnectionStrings:MsSqlConnection"]!,
				name: "Microsoft SQL",
				failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
				tags: ["services", "ready"])
#endif
#if (CacheSqlServer)
			.AddSqlServer(
				connectionString: builder.Configuration["ConnectionStrings:MsSqlCacheConnection"]!,
				name: "Microsoft SQL (Cache)",
				failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
				tags: ["services", "ready"])
#endif
#if (CacheRedis)
			.AddRedis(
				redisConnectionString: builder.Configuration["ConnectionStrings:RedisConnection"]!,
				name: "Redis",
				failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
				tags: ["services", "ready"])
#endif
			.AddRabbitMQ(
				rabbitConnectionString: builder.Configuration["ConnectionStrings:RabbitMQConnection"]!,
				name: "RabbitMQ",
				failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
				tags: ["services", "ready"])
			;

		return builder;
	}

	//! do not change the order of these calls without understanding their dependencies.
	public static WebApplication ConfigureDefaultPipeline(this WebApplication app)
	{
		app.UseForwardedHeaders();

		if (app.Environment.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}
		else
		{
			app.UseExceptionHandler();
			app.UseHsts();
			app.UseHttpsRedirection();
		}

		app.UseStatusCodePages();
		app.UseStaticFiles();

		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();

		app.UseResponseCaching();
		app.UseResponseCompression();

		// app.UseCors();

		app.MapPrometheusScrapingEndpoint();
		app.MapPingEndpoint();
		app.MapMeEndpoint();

		// Health check endpoints (live/ready/startup triad).
		var liveOptions = new HealthCheckOptions { Predicate = _ => false };
		var readyOptions = new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") };
		var startupOptions = new HealthCheckOptions { Predicate = h => h.Tags.Contains("startup") };
		var healthGroup = app.MapGroup("").AllowAnonymous();
		healthGroup.MapHealthChecks("/health");
		healthGroup.MapHealthChecks("/healthz", readyOptions);
		healthGroup.MapHealthChecks("/healthz/ready", readyOptions);
		healthGroup.MapHealthChecks("/healthz/live", liveOptions);
		healthGroup.MapHealthChecks("/healthz/startup", startupOptions);

		app.UseMiddlewareForFeature<AppEnvironmentResponseHeadersMiddleware>(AppEnvironmentResponseHeadersSettings.FeatureFlag);

		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
			app.MapScalarApiReference(options =>
			{
				options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
				options.EnabledTargets = [ScalarTarget.CSharp, ScalarTarget.PowerShell];
				options.EnabledClients = [ScalarClient.HttpClient, ScalarClient.WebRequest];
			});
		}

		return app;
	}
}
