﻿using System;
using System.Threading.Tasks;
using Burcin.Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Ruya.Primitives;
using Serilog;
#if (HealthChecks)
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Bedia.Api.HealthChecks;
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
			services.AddMvc()
					.SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
			        .AddJsonOptions(options =>
			                        {
				                        options.SerializerSettings.Converters.Add(new StringEnumConverter());
				                        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
			                        });
			services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DomainMatcherPolicy.DomainMatcherPolicy>());
			services.AddResponseCaching();
            services.AddResponseCompression(options =>
            {
				#if (Blazor)
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    MediaTypeNames.Application.Octet,
                    WasmMediaTypeNames.Application.Wasm,
                });
				#endif
            });

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
				                                   check.AddDrive("C:\\"
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
						       , name: "Remote Services"
			                   , failureStatus: HealthStatus.Degraded
			                   , tags: new[]
			                           {
				                           "3rdParty"
			                           })

					#if (EntityFramework)
					// TODO: Make the `DefaultConnection` string constant. It exists in Program.cs too.
			        .AddSqlServer(connectionString: Configuration["ConnectionStrings:DefaultConnection"]
			                    , name: "Microsoft SQL"
			                    , tags: new[]
			                            {
				                            "services"
			                            })
					#endif
					#if (CacheSqlServer)
			        .AddSqlServer(connectionString: Configuration["ConnectionStrings:SqlCacheConnection"]
			                    , name: "Microsoft SQL (Cache)"
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
			                        , failureStatus: HealthStatus.Degraded
			                        , tags: new[]
			                                {
				                                "services"
			                                })
			        .AddApplicationInsightsPublisher();
			services.AddHealthChecksUI();
			#endif
		}

		public static void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime)
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
               app.UseHttpsRedirection();
			}

			app.UseResponseCompression();
			app.UseResponseCaching();

			#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
			app.Map("/liveness"
			      , lapp => lapp.Run(async ctx => ctx.Response.StatusCode = 200));
			#pragma warning restore CS1998

			#if (HealthChecks)
			//x app.UseHealthChecks("/health", 911);
			app.UseHealthChecks("/health", new HealthCheckOptions
			                               {
				                               Predicate = check => true,
			                               });

			app.UseHealthChecks("/health/ready", new HealthCheckOptions
			                                     {
				                                     Predicate = check => check.Tags.Contains("ready"),
			                                     });

			app.UseHealthChecks("/health/live", new HealthCheckOptions
			                                    {
				                                    Predicate = check => false,
			                                    });

			app.UseHealthChecks("/health/custom"
			                  , new HealthCheckOptions
			                    {
				                    Predicate = _ => true
				                  , ResponseWriter = CustomWriteResponse.WriteResponse
			                    });

			app.UseHealthChecks("/health/beatpulse"
			                  , new HealthCheckOptions
			                    {
				                    Predicate = check => true
				                   ,ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
			                    });

			app.UseHealthChecksUI(setup =>
						{
							setup.ApiPath = "/health/beatpulse-api";
							setup.UIPath = "/health/beatpulse-ui";
							setup.WebhookPath = "/health/beatpulse-webhooks";
						});
			#endif

			app.UseStartTimeHeader();
			app.UseRequestResponseLogging(); //x app.UseMiddleware<RequestResponseLoggingMiddleware>();

			app.UseMvc(routes =>
            {
                routes.MapRoute(name: "default", template: "{controller}/{action}/{id?}");
            });

			#if (BlazorApplication)
            app.UseBlazor<Web.Startup>();
			#endif

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
