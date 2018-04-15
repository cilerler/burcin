using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.HealthChecks;
using Ruya.Primitives;
using Ruya.ConsoleHost;
using Serilog;
using Burcin.Api.HealthChecks;
using Burcin.Api.Middlewares;
using Swashbuckle.AspNetCore.Swagger;

namespace Burcin.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            services.AddOptions();

            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            //services.AddDistributedSqlServerCache(options =>
            //                                      {
            //                                          options.ConnectionString = Configuration.GetConnectionString(Configuration.GetValue<string>("Cache:SqlServer:ConnectionStringKey"));
            //                                          options.SchemaName = Configuration.GetValue<string>("Cache:SqlServer:SchemaName");
            //                                          options.TableName = Configuration.GetValue<string>("Cache:SqlServer:TableName");
            //                                      });
            //services.AddDistributedRedisCache(options =>
            //                                  {
            //                                      options.Configuration = Configuration.GetConnectionString(Configuration.GetValue<string>("Cache:Redis:ConnectionStringKey"));
            //                                      options.InstanceName = Configuration.GetValue<string>("Cache:Redis:InstanceName");
            //                                      //x Configuration.GetSection("Cache:Redis").Bind(options);
            //                                  });

            services.AddMvc().AddJsonOptions(options =>
                                             {
                                                 options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                                                 options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                                             });

            services.AddSwaggerGen(options =>
                                   {
                                       options.DescribeAllEnumsAsStrings();
                                       options.IgnoreObsoleteActions();
                                       options.IgnoreObsoleteProperties();
                                       options.SwaggerDoc("v1", new Info { Title = "Burcin API", Version = "1.0", Description = "Burcin API", TermsOfService = "Terms Of Service" });
                                   });

            services.AddSingleton<CustomHealthCheck>();
            services.AddHealthChecks(HealthChecks);

            SerilogHelper.Register(Configuration);

            Program.RegisterExternalServices(services, Configuration);
        }

        private void HealthChecks(HealthCheckBuilder healthChecks)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            healthChecks.WithDefaultCacheDuration(Program.DefaultCacheDuration);
            healthChecks.AddHealthCheckGroup("memory"
                                           , group => group.AddPrivateMemorySizeCheck((long)Constants.MegaByte * 150)
                                                           .AddWorkingSetCheck((long)Constants.MegaByte * 100)
                                                           .AddVirtualMemorySizeCheck((long)Constants.TeraByte * 3)
                                           , CheckStatus.Unhealthy)
                        .AddSqlCheck("(databaseName)", connectionString)

                        .AddCheck<CustomHealthCheck>(nameof(CustomHealthCheck))
                        .AddCheck("long-running"
                                , async cancellationToken =>
                                  {
                                      await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                                      return HealthCheckResult.Healthy("OK");
                                  })
                        .AddValueTaskCheck("short-running", () => new ValueTask<IHealthCheckResult>(HealthCheckResult.Healthy("OK")))

                        .AddHealthCheckGroup("servers"
                                           , group => group.AddUrlCheck("https://nuget.org")
                                                           .AddUrlCheck("https://github.com"))
                        .AddUrlCheck("(repositoryUrl)")

                         // TODO relative URL is not working
                        //.AddUrlCheck("/liveness", TimeSpan.Zero)
                ;
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime)
        {
            applicationLifetime.ApplicationStopped.Register(ApplicationStoppedActions);

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

            app.UseSwagger();
            app.UseSwaggerUI(c =>
                             {
                                 c.SwaggerEndpoint("/swagger/v1/swagger.json", "Burcin");
                             });
            
            app.UseStartTimeHeader();

            #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            app.Map("/liveness", lapp => lapp.Run(async ctx => ctx.Response.StatusCode = 200));
            #pragma warning restore CS1998

            app.UseMvc();
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
                            await context.Response.WriteAsync($"<p>Listening on the following addresses: {string.Join(", ", serverAddressesFeature.Addresses)}<p>");
                        }

                        await context.Response.WriteAsync($"<p>Request URL: {context.Request.GetDisplayUrl()}</p>");
                    });
        }

        public void ApplicationStoppedActions() => Log.CloseAndFlush();

    }
}
