using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BurcinCo.BurcinApp.Domain;
#if (EntityFramework || OData)
using BurcinCo.BurcinApp.Data;
#endif
#if (EntityFramework)
using Microsoft.EntityFrameworkCore;
using Ruya.EntityFrameworkCore.SqlServer;
using Ruya.EntityFrameworkCore.SqlServer.BatchLock;
#endif
#if (OData)
using Microsoft.AspNetCore.OData;
#endif
#if (Kiota)
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
#endif

namespace BurcinCo.BurcinApp.Host;

internal static class ProgramExtensionsCustom
{
	public static IHostApplicationBuilder AddCustomServices(this IHostApplicationBuilder builder)
	{
#if (EntityFramework)
		const string databaseConnectionString = "MsSqlConnection";
		string connectionString = builder.Configuration.GetConnectionString(databaseConnectionString)!;
		string assemblyName = builder.Configuration.GetValue<string>(DbContextFactory.MigrationAssemblyNameConfiguration)!;
		builder.Services.AddDbContext<BurcinDatabaseDbContext>(options => options.UseSqlServer(connectionString,
			sqlServerOptions =>
			{
				sqlServerOptions.MigrationsAssembly(assemblyName);
				sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
			}));

		builder.Services.AddBatchLockOperations<BurcinDatabaseDbContext>();
		builder.Services.AddBulkInsertOperations<BurcinDatabaseDbContext>();
#endif

#if (OData)
		builder.Services.AddControllers()
			.AddOData(options => options
				.AddRouteComponents("odata", ODataEdmModelBuilder.GetEdmModel())
				.Select()
				.Expand()
				.Filter()
				.OrderBy()
				.Count()
				.SkipToken()
				.SetMaxTop(100));
		
		//builder.Services.AddODataServiceContextService();
#endif

#if (Kiota)
		// builder.Services.AddKiotaHandlers();
		// builder.Services.AddHttpClient<OperationsApiClientFactory>((sp, client) => {
		// 	var configuration = sp.GetRequiredService<IConfiguration>();
		// 	var endpoint = configuration.GetConnectionString("ApiEndPoint") ?? throw new ArgumentNullException("ApiEndPoint connection string is null");
		// 	client.BaseAddress = new Uri(endpoint);
		// }).AttachKiotaHandlers();
		// builder.Services.AddTransient(sp => sp.GetRequiredService<OperationsApiClientFactory>().GetClient());

		// // builder.Services.AddScoped<IAuthenticationProvider, AnonymousAuthenticationProvider>();
		// // builder.Services
		// // 	.AddHttpClient<IRequestAdapter, HttpClientRequestAdapter>(client =>
		// // 		client.BaseAddress = new Uri(builder.Configuration.GetConnectionString("ODataEndPoint")));
		// // builder.Services.AddScoped<Kiota.DatabaseApi.DatabaseApiClient>();
#endif

		return builder;
	}

	public static WebApplication ConfigureCustomPipeline(this WebApplication app)
	{
		app.MapControllers();
		app.MapDefaultControllerRoute();

		return app;
	}
}
