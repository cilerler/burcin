using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Burcin.Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Polly.Retry;
using Ruya.Primitives;
using Prometheus;
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
			services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DomainMatcherPolicy.DomainMatcherPolicy>());
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
															,
															Version = "1.0"
															,
															Description = "Burcin API"
															,
															TermsOfService = "Terms Of Service"
														});
									   // Set the comments path for the Swagger JSON and UI.
									   string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
									   string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
									   options.IncludeXmlComments(xmlPath);
								   });
#endif

#if (HealthChecks)
			AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
			   .HandleTransientHttpError()
			   .Or<TimeoutRejectedException>()
			   .RetryAsync(5);
			services.AddHttpClient("Remote Services").AddPolicyHandler(retryPolicy);

			services.AddSingleton<CustomHealthCheck>();
			services.AddHealthChecks()
					.AddCheck<CustomHealthCheck>(CustomHealthCheck.HealthCheckName
											   , failureStatus: HealthStatus.Degraded
											   , tags: new[]
													   {
														   "ready"
													   })
					.AddCheck<SlowDependencyHealthCheck>(SlowDependencyHealthCheck.HealthCheckName
													   , failureStatus: null
													   , tags: new[]
															   {
																   "ready"
															   })
					.AddAsyncCheck(name: "long_running"
								 , check: async cancellationToken =>
										  {
											  await Task.Delay(TimeSpan.FromSeconds(5)
															 , cancellationToken);
											  return HealthCheckResult.Healthy("OK");
										  }
								 , tags: new[]
										 {
											 "self"
										 })
					.AddWorkingSetHealthCheck((long)Constants.GigaByte * 1
											, name: "Memory (WorkingSet)"
											, failureStatus: HealthStatus.Degraded
											, tags: new[]
													{
														"self"
													})
					.AddDiskStorageHealthCheck(check =>
											   {
												   check.AddDrive(DriveInfo.GetDrives().First().Name
																, 1024);
											   }
											 , name: "Disk Storage"
											 , failureStatus: HealthStatus.Degraded
											 , tags: new[]
													 {
														 "self"
													 })
					.AddDnsResolveHealthCheck(setup => setup.ResolveHost("burcin.local")
											, name: "DNS"
											, failureStatus: HealthStatus.Degraded
											, tags: new[]
													{
														"self"
													})
					.AddPingHealthCheck(setup => setup.AddHost("burcin.local"
															 , (int)TimeSpan.FromSeconds(3)
																			.TotalMilliseconds)
									  , name: "Ping"
									  , failureStatus: HealthStatus.Degraded
									  , tags: new[]
											  {
												  "3rdParty"
											  })
					.AddUrlGroup(new[]
								 {
									 new Uri("https://burcin.local")
								 }
							   , name: "Remote Urls"
							   , failureStatus: HealthStatus.Degraded
							   , tags: new[]
									   {
										   "3rdParty"
									   })

				#if (EntityFramework)
					 // TODO: Make the `DefaultConnection` string constant. It exists in Program.cs too.
					 .AddSqlServer(connectionString: Configuration["ConnectionStrings:DefaultConnection"]
					 			, name: "Microsoft SQL"
								, failureStatus: HealthStatus.Unhealthy
					 			, tags: new[]
					 					{
					 						"services"
					 					})
				#endif
				#if (CacheSqlServer)
					.AddSqlServer(connectionString: Configuration["ConnectionStrings:SqlServerCacheConnection"]
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
					.AddRabbitMQ(rabbitMQConnectionString: Configuration["ConnectionStrings:RabbitMqConnection"]
							   , name: "RabbitMq"
							   , failureStatus: HealthStatus.Unhealthy
							   , tags: new[]
										{
											"services"
										})
					.AddElasticsearch(elasticsearchUri: Configuration["ConnectionStrings:ElasticSearchConnection"]
								, name: "ElasticSearch"
								, failureStatus: HealthStatus.Unhealthy
								, tags: new[]
										{
											"services"
										})
					//.AddApplicationInsightsPublisher()
					.AddSeqPublisher(options => options.Endpoint = Configuration["ConnectionStrings:SeqConnection"])
					//.AddPrometheusGatewayPublisher()
					;
			services.AddHealthChecksUI();
			#endif
		}

		public static void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseDatabaseErrorPage();
			}

			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
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
			// todo research why this part throws an error on Core 3
			//app.UseSwagger();
			//app.UseSwaggerUI(c =>
			//				 {
			//					 c.SwaggerEndpoint("/swagger/v1/swagger.json", "Burcin");
			//				 });
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
				endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
			});

			app.UseWelcomePage();

			// will never hit here if UseWelcomePage is not commented
			IServerAddressesFeature serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
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
							await context.Response.WriteAsync($"<p>Listening on the following addresses: {string.Join(", ", serverAddressesFeature.Addresses)}<p>");
						}
						await context.Response.WriteAsync($"<p>Request URL: {context.Request.GetDisplayUrl()}</p>");
					});
		}
	}
}
