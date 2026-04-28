using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
				})
				.EnableDetailedErrors()
#if DEBUG
				.EnableSensitiveDataLogging()
#endif
			;
		});

		return services;
	}
}
