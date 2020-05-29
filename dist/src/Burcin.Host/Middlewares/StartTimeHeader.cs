using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Burcin.Host.Middlewares
{
    public class StartTimeHeader
    {
        public const string InMemoryCacheKey = "serverStartTime";
        public const string DistributedCacheKey = "lastServerStartTime";

        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;

        public StartTimeHeader(RequestDelegate next, ILogger<StartTimeHeader> logger, IMemoryCache memoryCache, IDistributedCache distributedCache)
        {
            _next = next;
			_logger = logger;
			_memoryCache = memoryCache;
            _distributedCache = distributedCache;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            const string notFound = "Not Found.";

            #region inmemory
	        string serverStartTimeOutput = _memoryCache.TryGetValue(InMemoryCacheKey, out DateTimeOffset serverStartTime)
		                                       ? serverStartTime.ToString("s")
		                                       : DateTimeOffset.UtcNow.ToString("s");
            httpContext.Response.Headers.Append("Server-Start-Time", serverStartTimeOutput);
            #endregion

            #region distributed
			byte[] distributedStartTime = null;
			try
			{
				distributedStartTime = await _distributedCache.GetAsync(DistributedCacheKey);
			}
			catch (Exception e)
			{
				_logger.LogError(e, e.Message);
			}
            string distributedStartTimeOutput = distributedStartTime == null
	                                                ? notFound
	                                                : Encoding.UTF8.GetString(distributedStartTime);
            httpContext.Response.Headers.Append("Last-Server-Start-Time", distributedStartTimeOutput);
            #endregion

            await _next.Invoke(httpContext);
        }
    }
}
