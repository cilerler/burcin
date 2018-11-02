using Microsoft.AspNetCore.Builder;

namespace Burcin.Api.Middlewares
{
    public static class Extensions
    {
        public static IApplicationBuilder UseStartTimeHeader(this IApplicationBuilder builder) => builder.UseMiddleware<StartTimeHeader>();
        public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder builder) => builder.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}