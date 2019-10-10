using Microsoft.AspNetCore.Builder;

namespace Burcin.Api.Middlewares
{
    public static class Extensions
    {
        public static IApplicationBuilder UseStartTimeHeader(this IApplicationBuilder builder) => builder.UseMiddleware<StartTimeHeader>();
        public static IApplicationBuilder UseApplicationInfoHeaders(this IApplicationBuilder builder) => builder.UseMiddleware<ApplicationInfoHeaders>();
    }
}
