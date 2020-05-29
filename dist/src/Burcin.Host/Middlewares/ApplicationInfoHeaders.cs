using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Burcin.Host.Middlewares
{
    public class ApplicationInfoHeaders
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;

        public ApplicationInfoHeaders(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Append("ApplicationVersion", Assembly.GetEntryAssembly()?.GetName().Version.ToString());
            httpContext.Response.Headers.Append("ApplicationName", _env.ApplicationName);
            httpContext.Response.Headers.Append("MachineName", Environment.MachineName);
            httpContext.Response.Headers.Append("Environment", _env.EnvironmentName);
            await _next.Invoke(httpContext);
        }
    }
}
