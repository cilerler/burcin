using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Burcin.Api.Middlewares;
using Burcin.Data;
using Burcin.Domain;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Edm;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Polly.Retry;
using Prometheus;
#if (CacheRedis)
using StackExchange.Redis;
#endif
#if (HealthChecks)
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using HealthChecks.UI.Client;
using Burcin.Api.HealthChecks;
#endif
#if (Swagger)
using Swashbuckle.AspNetCore.Swagger;
#endif

namespace Burcin.Api
{
	public class Startup
	{
		public IConfiguration Configuration { get; }

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers()
					.AddNewtonsoftJson(options =>
									{
										options.SerializerSettings.Converters.Add(new StringEnumConverter());
										options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
									});

			services.AddOData();

			services.AddOptions();
			services.AddMemoryCache();
#if (!CacheExists)
			services.AddDistributedMemoryCache();
#endif

#if (CacheSqlServer)
			services.AddDistributedSqlServerCache(options =>
			{
				options.ConnectionString = Configuration.GetConnectionString(Configuration.GetValue<string>("Cache:SqlServer:ConnectionStringKey"));
				options.SchemaName = Configuration.GetValue<string>("Cache:SqlServer:SchemaName");
				options.TableName = Configuration.GetValue<string>("Cache:SqlServer:TableName");
			});
#endif
#if (CacheRedis)
			services.AddStackExchangeRedisCache(options =>
			{
				options.Configuration = Configuration.GetConnectionString(Configuration.GetValue<string>("Cache:Redis:ConnectionStringKey"));
				options.InstanceName = Configuration.GetValue<string>("Cache:Redis:InstanceName");
				//x Configuration.GetSection("Cache:Redis").Bind(options);
				options.ConfigurationOptions =
					new ConfigurationOptions
					{
						AbortOnConnectFail = true
					};
			});
#endif

#if (EntityFramework)
			const string databaseConnectionString = "MsSqlConnection";
			string connectionString = Configuration.GetConnectionString(databaseConnectionString);
			string assemblyName = Configuration.GetValue(typeof(string)
																   , DbContextFactory.MigrationAssemblyNameConfiguration)
											 .ToString();
			services.AddDbContext<BurcinDatabaseDbContext>(options => options.UseSqlServer(connectionString
																				 , sqlServerOptions =>
																				 {
																					 sqlServerOptions.MigrationsAssembly(assemblyName);
																					 sqlServerOptions.EnableRetryOnFailure(5
																																						   , TimeSpan.FromSeconds(30)
																																						   , null);
																				 }));
#endif

#if (BackgroundService)
			services.AddGracePeriodManagerService(Configuration);
#endif

			services.AddHelper(Configuration);

			services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DomainMatcherPolicy.DomainMatcherPolicy>());
			services.AddResponseCaching();
			services.AddResponseCompression();

#if (BlazorApplication)
			services.AddRazorPages();
			services.AddServerSideBlazor();
#endif

#if (Swagger)
			services.AddSwaggerGen(options =>
								   {
									   options.IgnoreObsoleteActions();
									   options.IgnoreObsoleteProperties();
									   options.SwaggerDoc("v1"
														, new OpenApiInfo
														{
															Title = "Burcin API"
															,
															Version = "1.0"
															,
															Description = "Burcin API"
														});
									   // Set the comments path for the Swagger JSON and UI.
									   string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
									   string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
									   options.IncludeXmlComments(xmlPath);
								   });
			// Workaround: https://github.com/OData/WebApi/issues/1177
			SetOutputFormatters(services);
#endif

#if (HealthChecks)
			AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
			   .HandleTransientHttpError()
			   .Or<TimeoutRejectedException>()
			   .RetryAsync(5);
			services.AddHttpClient("Remote Services").AddPolicyHandler(retryPolicy);

			services.AddSingleton<CustomHealthCheck>();
			services.AddHealthChecks()
					// .AddCheck<CustomHealthCheck>(CustomHealthCheck.HealthCheckName
					// 						   , failureStatus: HealthStatus.Degraded
					// 						   , tags: new[]
					// 								   {
					// 									   "ready"
					// 								   })
					// .AddCheck<SlowDependencyHealthCheck>(SlowDependencyHealthCheck.HealthCheckName
					// 								   , failureStatus: null
					// 								   , tags: new[]
					// 										   {
					// 											   "ready"
					// 										   })
					// .AddAsyncCheck(name: "long_running"
					// 			 , check: async cancellationToken =>
					// 					  {
					// 						  await Task.Delay(TimeSpan.FromSeconds(5)
					// 										 , cancellationToken);
					// 						  return HealthCheckResult.Healthy("OK");
					// 					  }
					// 			 , tags: new[]
					// 					 {
					// 						 "self"
					// 					 })
					// .AddWorkingSetHealthCheck((long)Ruya.Primitives.Constants.GigaByte * 1
					// 						, name: "Memory (WorkingSet)"
					// 						, failureStatus: HealthStatus.Degraded
					// 						, tags: new[]
					// 								{
					// 									"self"
					// 								})
					// .AddDiskStorageHealthCheck(check =>
					// 						   {
					// 							   check.AddDrive(DriveInfo.GetDrives().First().Name
					// 											, 1024);
					// 						   }
					// 						 , name: "Disk Storage"
					// 						 , failureStatus: HealthStatus.Degraded
					// 						 , tags: new[]
					// 								 {
					// 									 "self"
					// 								 })
					// .AddDnsResolveHealthCheck(setup => setup.ResolveHost("burcin.local")
					// 						, name: "DNS"
					// 						, failureStatus: HealthStatus.Degraded
					// 						, tags: new[]
					// 								{
					// 									"self"
					// 								})
					// .AddPingHealthCheck(setup => setup.AddHost("burcin.local"
					// 										 , (int)TimeSpan.FromSeconds(3)
					// 														.TotalMilliseconds)
					// 				  , name: "Ping"
					// 				  , failureStatus: HealthStatus.Degraded
					// 				  , tags: new[]
					// 						  {
					// 							  "3rdParty"
					// 						  })
					// .AddUrlGroup(new[]
					// 			 {
					// 				 new Uri("https://burcin.local")
					// 			 }
					// 		   , name: "Remote Urls"
					// 		   , failureStatus: HealthStatus.Degraded
					// 		   , tags: new[]
					// 				   {
					// 					   "3rdParty"
					// 				   })

				#if (EntityFramework)
					 // TODO: Make the `MsSqlConnection` string constant. It exists in Program.cs too.
					 .AddSqlServer(connectionString: Configuration["ConnectionStrings:MsSqlConnection"]
					 			, name: "Microsoft SQL"
								, failureStatus: HealthStatus.Unhealthy
					 			, tags: new[]
					 					{
					 						"services"
					 					})
				#endif
				#if (CacheSqlServer)
					.AddSqlServer(connectionString: Configuration["ConnectionStrings:MsSqlCacheConnection"]
								, name: "Microsoft SQL (Cache)"
								, failureStatus: HealthStatus.Degraded
								, tags: new[]
										{
										 	"services"
										})
				#endif
				#if (CacheRedis)
					.AddRedis(redisConnectionString: Configuration["ConnectionStrings:RedisCacheConnection"]
								, name: "Redis"
								, failureStatus: HealthStatus.Degraded
								, tags: new[]
									{
										"services"
									})
				#endif
					// .AddRabbitMQ(rabbitMQConnectionString: Configuration["ConnectionStrings:RabbitMqConnection"]
					// 		   , name: "RabbitMq"
					// 		   , failureStatus: HealthStatus.Unhealthy
					// 		   , tags: new[]
					// 					{
					// 						"services"
					// 					})
					// .AddElasticsearch(elasticsearchUri: Configuration["ConnectionStrings:ElasticSearchConnection"]
					// 			, name: "ElasticSearch"
					// 			, failureStatus: HealthStatus.Unhealthy
					// 			, tags: new[]
					// 					{
					// 						"services"
					// 					})
					//.AddApplicationInsightsPublisher()
					//.AddPrometheusGatewayPublisher()
					//.AddSeqPublisher(options => options.Endpoint = Configuration["ConnectionStrings:SeqConnection"])
					;
			services.AddHealthChecksUI();
		#endif
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseDatabaseErrorPage();
			}
			else
			{
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}
			app.UseHttpsRedirection();
			app.UseResponseCompression();
			app.UseResponseCaching();
			app.UseStaticFiles();
			app.UseRouting();
			app.UseCors();
			app.UseAuthentication();
			app.UseAuthorization();
			app.UseStatusCodePages();

			app.UseStartTimeHeader();
			app.UseApplicationInfoHeaders();

			Counter counter = Metrics.CreateCounter("PathCounter", "Counts requests to endpoints", new CounterConfiguration { LabelNames = new[] { "method", "endpoint" } });
			app.Use((context, next) =>
			{
				counter.WithLabels(context.Request.Method, context.Request.Path).Inc();
				return next();
			});
			app.UseMetricServer("/metrics");
			app.UseHttpMetrics();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
			app.Map("/liveness", lapp => lapp.Run(async ctx => ctx.Response.StatusCode = 200));
#pragma warning restore CS1998

			app.UseExceptionHandler(ex =>
			{
				ex.Run(async context =>
				{
					context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
					string result;
					IExceptionHandlerPathFeature exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
					if (exceptionHandlerPathFeature == null)
					{
						result = string.Empty;
						context.Response.ContentType = "text/plain";
					}
					else
					{
						result = env.IsDevelopment()
							? JsonConvert.SerializeObject(exceptionHandlerPathFeature)
							: JsonConvert.SerializeObject(new
							{
								Error = new { exceptionHandlerPathFeature.Error.Message },
								exceptionHandlerPathFeature.Path
							});
						context.Response.ContentType = "application/json";
					}
					await context.Response.WriteAsync(result);
				});
			});

#if (Swagger)
			app.UseSwagger();
			app.UseSwaggerUI(c =>
							 {
								 c.SwaggerEndpoint("/swagger/v1/swagger.json", "Burcin");
							 });
#endif

			app.UseEndpoints(endpoints =>
			{
#if (HealthChecks)
				endpoints.MapHealthChecks("/health", new HealthCheckOptions {Predicate = check => true,});

				endpoints.MapHealthChecks("/health/ready",
					new HealthCheckOptions {Predicate = check => check.Tags.Contains("ready"),});

				endpoints.MapHealthChecks("/health/live", new HealthCheckOptions {Predicate = check => false,});

				endpoints.MapHealthChecks("/health/custom",
					new HealthCheckOptions {Predicate = _ => true, ResponseWriter = CustomWriteResponse.WriteResponse});

				endpoints.MapHealthChecks("/healthz",
					new HealthCheckOptions
					{
						Predicate = check => true,
						ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
						ResultStatusCodes =
						{
							[HealthStatus.Healthy] = StatusCodes.Status200OK,
							[HealthStatus.Degraded] = StatusCodes.Status200OK,
							[HealthStatus.Unhealthy] = StatusCodes.Status200OK
						}
					});

				endpoints.MapHealthChecksUI(setup =>
				{
					setup.ApiPath = "/healthchecks-api"; // "/health/beatpulse-api";
					setup.UIPath = "/healthchecks-ui"; // "/health/beatpulse-ui";
					setup.WebhookPath = "/healthchecks-webhooks"; // "/health/beatpulse-webhooks";
					setup.AddCustomStylesheet("dotnet.css");
				});
#endif

				endpoints.MapControllers();
				endpoints.SetDefaultQuerySettings(new Microsoft.AspNet.OData.Query.DefaultQuerySettings
				{
					EnableSelect = true,
					EnableExpand = true,
					EnableFilter = true,
					EnableOrderBy = true,
					EnableCount = true,
					EnableSkipToken = true,
					MaxTop = 100
				});
				endpoints.MapODataRoute("odata", "odata", ODataEdmModelHelper.GetEdmModel());
				endpoints.MapDefaultControllerRoute();
#if (BlazorApplication)
				endpoints.MapBlazorHub();
				endpoints.MapFallbackToPage("/_Host");
#endif
			});

			app.UseWelcomePage();

			// will never hit here if UseWelcomePage is not commented
			IServerAddressesFeature serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
			app.Run(async context =>
					{
						context.Response.ContentType = "text/html";
						await context.Response.WriteAsync("<p>Hosted by Kestrel<p>");
						if (Environment.GetEnvironmentVariable("DOTNET_PORT") != null)
						{
							await context.Response.WriteAsync("Using IIS as reverse proxy.");
						}
						if (serverAddressesFeature != null)
						{
							await context.Response.WriteAsync($"<p>Listening on the following addresses: {string.Join(", ", serverAddressesFeature.Addresses)}<p>");
						}
						await context.Response.WriteAsync($"<p>Request URL: {context.Request.GetDisplayUrl()}</p>");
					});
		}

#if (Swagger)
		private void SetOutputFormatters(IServiceCollection services)
		{
			services.AddMvcCore(options =>
			{
				foreach (var outputFormatter in options.OutputFormatters.OfType<ODataOutputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
				{
					outputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/odata"));
				}
				foreach (var inputFormatter in options.InputFormatters.OfType<ODataInputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
				{
					inputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/odata"));
				}
			});
		}
#endif
	}
}
