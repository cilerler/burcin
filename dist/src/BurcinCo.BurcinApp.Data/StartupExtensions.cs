using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
#if (Sample)
using Ruya.Services.ReliableMessaging.EntityFrameworkCore;
using Ruya.Services.ReliableMessaging.EntityFrameworkCore.Extensions;
using Ruya.Services.ReliableMessaging.Extensions;
#endif

namespace BurcinCo.BurcinApp.Data;

public sealed class SqlServerOptions
{
	public int? CommandTimeoutSeconds { get; init; } = (int)TimeSpan.FromSeconds(30).TotalSeconds;
	public bool EnableRetryOnFailure { get; init; } = true;
	public int MaxRetryCount { get; init; } = 6;
	public int MaxRetryDelaySeconds { get; init; } = (int)TimeSpan.FromSeconds(30).TotalSeconds;
}

public sealed class BurcinDatabaseDbContextSettings
{
	public const string ConfigurationSectionName = "Database";

	/// <summary>
	/// The connection string key to look up in configuration.
	/// If the connection string is empty or not found, the DbContext will be configured
	/// for metadata-only mode (no actual database connection).
	/// </summary>
	public string ConnectionStringKey { get; init; } = "MsSqlConnection";
	public SqlServerOptions SqlServerOptions { get; init; } = new();

	/// <summary>
	/// Optional override for the EF migrations-assembly name. Production leaves this null (migrations
	/// are applied via the EF CLI which pins the assembly through --project), but test fixtures need
	/// to point at the generated <c>{Org}.{Project}.Migrations</c> assembly so <c>MigrateAsync()</c>
	/// against a Testcontainer picks up the right migration set.
	/// </summary>
	public string? MigrationsAssemblyName { get; set; }
}

public static class StartupExtensions
{
	public static IServiceCollection AddBurcinDatabaseDbContext(
		this IServiceCollection services,
		Action<BurcinDatabaseDbContextSettings>? setupAction = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddOptions<BurcinDatabaseDbContextSettings>()
			.BindConfiguration(BurcinDatabaseDbContextSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		if (setupAction is not null)
		{
			services.Configure(setupAction);
		}

		// Stateless interceptor; resolved into the DbContextOptions below via AddInterceptors.
		// Singleton because DbContextOptions is singleton and the interceptor holds no state.
		// Works alongside the DB-side INSTEAD OF DELETE triggers in tools/EntityFramework/triggers.sql:
		// the interceptor catches EF-tracker DELETEs (Remove + SaveChanges) so EF's optimistic-concurrency
		// OUTPUT clause doesn't collide with the trigger (SQL Server error 334); the triggers catch raw-SQL
		// DELETEs (maintenance scripts, sqlcmd, other services). Together they cover every delete path.
		services.AddSingleton<SoftDeleteSaveChangesInterceptor>();

		services.AddDbContext<BurcinDatabaseDbContext>((serviceProvider, options) =>
		{
			var settings = serviceProvider.GetRequiredService<IOptions<BurcinDatabaseDbContextSettings>>().Value;
			var configuration = serviceProvider.GetRequiredService<IConfiguration>();

			var connectionString = configuration.GetConnectionString(settings.ConnectionStringKey);

			// Allow empty/missing connection string for metadata-only scenarios (no actual DB connection)
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				options.UseSqlServer(string.Empty);
				return;
			}

			options.UseSqlServer(
				connectionString,
				sql =>
				{
					if (settings.SqlServerOptions.CommandTimeoutSeconds.HasValue)
						sql.CommandTimeout(settings.SqlServerOptions.CommandTimeoutSeconds.Value);

					if (settings.SqlServerOptions.EnableRetryOnFailure)
					{
						sql.EnableRetryOnFailure(
							maxRetryCount: settings.SqlServerOptions.MaxRetryCount,
							maxRetryDelay: TimeSpan.FromSeconds(settings.SqlServerOptions.MaxRetryDelaySeconds),
							errorNumbersToAdd: null);
					}

					// Test fixtures pin this to the generated Migrations project so MigrateAsync() works
					// against a Testcontainer. Production leaves it null — migrations are applied via the
					// EF CLI which pins through --project, not through the runtime DbContext options.
					if (!string.IsNullOrWhiteSpace(settings.MigrationsAssemblyName))
					{
						sql.MigrationsAssembly(settings.MigrationsAssemblyName);
					}
				})
				.EnableDetailedErrors()
				.AddInterceptors(serviceProvider.GetRequiredService<SoftDeleteSaveChangesInterceptor>())
			;
			EnableSensitiveDataLoggingInDebug(options);

			// Apply any registered cross-cutting configurers. Preserved as a seam for runtime opt-ins.
			// When Sample is on, AddBurcinDatabaseReliableMessaging registers a configurer that wires
			// the Outbox interceptor here. Schema (Outbox/Inbox model) is registered separately in
			// _BurcinDatabaseDbContext.OnModelCreatingPostActions so the model matches the migration
			// regardless of whether reliable-messaging is opted into at this call site.
			foreach (var configurer in serviceProvider.GetServices<IDbContextConfigurer<BurcinDatabaseDbContext>>())
			{
				configurer.Configure(serviceProvider, options);
			}
		});

		return services;
	}

	// A normal DEBUG preprocessor block is interpreted by `dotnet new` as a template condition and
	// disappears from generated projects. ConditionalAttribute preserves the compiler-only Debug behavior
	// without colliding with the template engine's C# conditional processor.
	[System.Diagnostics.Conditional("DEBUG")]
	private static void EnableSensitiveDataLoggingInDebug(DbContextOptionsBuilder optionsBuilder)
	{
		ArgumentNullException.ThrowIfNull(optionsBuilder);
		optionsBuilder.EnableSensitiveDataLogging();
	}

	#if (Sample)
	/// <summary>
	/// Persistence-side reliable-messaging wiring. Chains onto an existing <c>IReliableMessagingBuilder</c>
	/// (Host calls <c>AddReliableMessaging()</c> once at app level — Polly throws on duplicate
	/// ResiliencePipeline keys, so we accept a builder rather than create our own). Registers the
	/// Outbox/Inbox EF stores against the shared <see cref="BurcinDatabaseDbContext"/>, the outbox
	/// health check tagged <c>"ready"</c>, and an <c>IDbContextConfigurer&lt;BurcinDatabaseDbContext&gt;</c>
	/// that adds the <c>OutboxSavingChangesInterceptor</c> to the DbContext options via Data's seam.
	///
	/// Tests that exercise the outbox path (Sourcing) call this; tests that don't (Recipe, Nutrition)
	/// skip it — their DbContext model still includes Outbox/Inbox via OnModelCreatingPostActions, but
	/// the interceptor isn't wired so SaveChanges doesn't try to flush anything.
	/// </summary>
	public static IReliableMessagingBuilder AddBurcinDatabaseReliableMessaging(this IReliableMessagingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Configurer that adds the Outbox interceptor to DbContext options. Resolved by the seam loop
		// inside AddBurcinDatabaseDbContext. Singleton — stateless.
		builder.Services.AddSingleton<IDbContextConfigurer<BurcinDatabaseDbContext>, OutboxInterceptorConfigurer>();

		return builder
			.AddOutboxContext<BurcinDatabaseDbContext>()
			.AddInboxContext<BurcinDatabaseDbContext>()
			.AddEntityFrameworkOutboxStore<BurcinDatabaseDbContext>()
			.AddEntityFrameworkInboxStore<BurcinDatabaseDbContext>()
			.AddOutboxHealthCheck<BurcinDatabaseDbContext>(tags: new[] { "ready" });
	}

	/// <summary>
	/// Minimal <see cref="IDbContextConfigurer{TContext}"/> that wires the
	/// <see cref="OutboxSavingChangesInterceptor{TContext}"/> into the DbContext options. Schema is
	/// registered separately in <c>BurcinDatabaseDbContext.OnModelCreatingPostActions</c>; this
	/// configurer's only job is the interceptor — keeps the model-vs-behavior split clean.
	/// </summary>
	internal sealed class OutboxInterceptorConfigurer : IDbContextConfigurer<BurcinDatabaseDbContext>
	{
		public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
		{
			ArgumentNullException.ThrowIfNull(serviceProvider);
			ArgumentNullException.ThrowIfNull(optionsBuilder);

			var outboxInterceptor = serviceProvider
				.GetRequiredService<OutboxSavingChangesInterceptor<BurcinDatabaseDbContext>>();
			optionsBuilder.AddInterceptors(outboxInterceptor);
		}
	}
	#endif
}
