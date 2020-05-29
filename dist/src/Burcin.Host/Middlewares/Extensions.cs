using Microsoft.AspNetCore.Builder;

namespace Burcin.Host.Middlewares
{
    public static class Extensions
    {
        public static IApplicationBuilder UseStartTimeHeader(this IApplicationBuilder builder) => builder.UseMiddleware<StartTimeHeader>();
        public static IApplicationBuilder UseApplicationInfoHeaders(this IApplicationBuilder builder) => builder.UseMiddleware<ApplicationInfoHeaders>();
    }
}
