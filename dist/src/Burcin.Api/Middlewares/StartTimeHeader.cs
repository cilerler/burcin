using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Burcin.Api.Middlewares
{
    public class StartTimeHeader
    {
        public const string InMemoryCacheKey = "serverStartTime";
        public const string DistributedCacheKey = "lastServerStartTime";

        private readonly RequestDelegate _next;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;

        public StartTimeHeader(RequestDelegate next, IMemoryCache memoryCache, IDistributedCache distributedCache)
        {
            _next = next;
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            const string notFound = "Not Found.";

            #region inmemory
            string serverStartTimeOutput = notFound;
            if (_memoryCache.TryGetValue(InMemoryCacheKey, out DateTimeOffset serverStartTime))
            {
                serverStartTimeOutput = serverStartTime.ToString("s");
            }
            httpContext.Response.Headers.Append("Server-Start-Time", serverStartTimeOutput);
            #endregion

            #region distributed
            string distributedStartTimeOutput = notFound;
            var distributedStartTime = await _distributedCache.GetAsync(DistributedCacheKey);
            if (distributedStartTime != null)
            {
                distributedStartTimeOutput = Encoding.UTF8.GetString(distributedStartTime);
            }
            httpContext.Response.Headers.Append("Last-Server-Start-Time", distributedStartTimeOutput);
            #endregion

            await _next.Invoke(httpContext);
        }
    }
}
