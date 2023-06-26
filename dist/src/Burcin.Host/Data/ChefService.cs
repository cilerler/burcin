using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
#if (EntityFramework)
using Burcin.Models.BurcinDatabase;
#else
using Burcin.Host.Data.Models;
#endif
using Microsoft.AspNetCore.Http;

namespace Burcin.Host.Data
{
    public class ChefService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;

		public ChefService(IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory)
		{
			_httpContextAccessor = httpContextAccessor;
			_clientFactory = clientFactory;
		}

        public async Task<ODataPagedResponse<Chef>> GetChefsAsync(int page, int pageSize)
        {
            if (page < 1)
            {
                throw new ArgumentException("Page number can not be less than 1");
            }

            if (pageSize < 2)
            {
                throw new ArgumentException("Page size can not be less than 2");
            }
            int skip = Math.Max((page - 1) * pageSize,0);
            int top = pageSize;
            var context = _httpContextAccessor.HttpContext;
            string baseUrl = $"{context.Request.Scheme}://{context.Request.Host}/odata/Chef?$count=true";//&$orderby=Id&$expand=org,contentTypeNavigation,statusNavigation";

            var output = new ODataPagedResponse<Chef>();
            var requestUrl = $"{baseUrl}&$skip={skip}&$top={top}";
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            requestMessage.Headers.Add("Accept", "application/json");

            using (var client = _clientFactory.CreateClient())
            {
                try
                {
                    var response =await client.SendAsync(requestMessage).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        try
                        {
                            output = JsonSerializer.Deserialize<ODataPagedResponse<Chef>>(jsonString, new JsonSerializerOptions { IgnoreNullValues = true, PropertyNameCaseInsensitive = true });
                            output.PageSize = pageSize;
                            output.CurrentPage = page;
                            return output;
                        }
                        catch (Exception e)
                        {
                            throw;
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
						return output;
                    }
                    else if (response.StatusCode==System.Net.HttpStatusCode.BadRequest)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        string message = null;
                        using (JsonDocument doc = JsonDocument.Parse(jsonString))
                        {
                            if (doc.RootElement.TryGetProperty("message", out JsonElement value))
                            {
                                message = value.GetString();
                            }
                        }
                        throw new ApplicationException(message);
                    }
                    else
                    {
                        var e = new HttpRequestException("Request failed.");
                        e.Data.Add("response",response);
                        throw e;
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }
    }
}
