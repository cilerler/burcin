using Microsoft.AspNetCore.Builder;

namespace Burcin.Api.Middlewares
{
    public static class Extensions
    {
        public static IApplicationBuilder UseStartTimeHeader(this IApplicationBuilder builder) => builder.UseMiddleware<StartTimeHeader>();
    }
}