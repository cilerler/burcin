using System;
using System.Threading.Tasks;
using Burcin.Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Ruya.Primitives;
using Serilog;
#if (HealthChecks)
using Microsoft.Extensions.HealthChecks;
using Burcin.Api.HealthChecks;
#endif
#if (Swagger)
using Swashbuckle.AspNetCore.Swagger;

#endif

namespace Burcin.Api
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddMvc()
			        .AddJsonOptions(options =>
			                        {
				                        options.SerializerSettings.Converters.Add(new StringEnumConverter());
				                        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
			                        });

			services.AddResponseCaching();
			services.AddResponseCompression();

			#if (Swagger)
			services.AddSwaggerGen(options =>
			                       {
				                       options.DescribeAllEnumsAsStrings();
				                       options.IgnoreObsoleteActions();
				                       options.IgnoreObsoleteProperties();
				                       options.SwaggerDoc("v1"
				                                        , new Info
				                                          {
					                                          Title = "Burcin API"
					                                        , Version = "1.0"
					                                        , Description = "Burcin API"
					                                        , TermsOfService = "Terms Of Service"
				                                          });
			                       });
			#endif

			#if (HealthChecks)
			services.AddSingleton<CustomHealthCheck>();
			services.AddHealthChecks(healthChecks =>
			                         {
				                         healthChecks.WithDefaultCacheDuration(Program.DefaultCacheDuration);
				                         healthChecks.AddHealthCheckGroup("memory"
				                                                        , group => group.AddPrivateMemorySizeCheck((long)Constants.MegaByte * 150)
				                                                                        .AddWorkingSetCheck((long)Constants.MegaByte * 100)
				                                                                        .AddVirtualMemorySizeCheck((long)Constants.TeraByte * 3)
				                                                        , CheckStatus.Unhealthy)
				                                      #if (EntityFramework)
				                                     .AddSqlCheck("(databaseName)"
				                                                , Program.DatabaseConnectionString)
				                                      #endif
				                                     .AddCheck<CustomHealthCheck>(nameof(CustomHealthCheck))
				                                     .AddCheck("long-running"
				                                             , async cancellationToken =>
				                                               {
					                                               await Task.Delay(TimeSpan.FromSeconds(5)
					                                                              , cancellationToken);
					                                               return HealthCheckResult.Healthy("OK");
				                                               })
				                                     .AddValueTaskCheck("short-running"
				                                                      , () => new ValueTask<IHealthCheckResult>(HealthCheckResult.Healthy("OK")))
				                                     .AddHealthCheckGroup("servers"
				                                                        , group => group.AddUrlCheck("https://nuget.org")
				                                                                        .AddUrlCheck("https://github.com"))
				                                     .AddUrlCheck("(repositoryUrl)")

					                         // TODO relative URL is not working
					                         //.AddUrlCheck("/liveness", TimeSpan.Zero)
					                         ;
			                         });
			#endif
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime)
		{
			if (env.IsDevelopment())
			{
				app.UseBrowserLink();
				app.UseDeveloperExceptionPage();
				app.UseDatabaseErrorPage();
			}
			//else
			//{
			//    app.UseExceptionHandler("/Home/Error");
			//}
			app.UseResponseCompression();
			app.UseResponseCaching();

			app.UseStartTimeHeader();

			#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
			app.Map("/liveness"
			      , lapp => lapp.Run(async ctx => ctx.Response.StatusCode = 200));
			#pragma warning restore CS1998

			app.UseMvc();

			#if (Swagger)
			app.UseSwagger();
			app.UseSwaggerUI(c =>
			                 {
				                 c.SwaggerEndpoint("/swagger/v1/swagger.json"
				                                 , "Burcin");
			                 });
			#endif

			app.UseWelcomePage();
			app.UseStatusCodePages();

			var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
			app.Run(async context =>
			        {
				        context.Response.ContentType = "text/html";
				        await context.Response.WriteAsync("<p>Hosted by Kestrel<p>");
				        if (Environment.GetEnvironmentVariable("ASPNETCORE_PORT") != null)
				        {
					        await context.Response.WriteAsync("Using IIS as reverse proxy.");
				        }

				        if (serverAddressesFeature != null)
				        {
					        await context.Response.WriteAsync($"<p>Listening on the following addresses: {string.Join(", " , serverAddressesFeature.Addresses)}<p>");
				        }

				        await context.Response.WriteAsync($"<p>Request URL: {context.Request.GetDisplayUrl()}</p>");
			        });
		}
	}
}
