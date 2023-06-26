using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Burcin.Host.Data;
#if (EntityFramework)
using Burcin.Models.BurcinDatabase;
#else
using Burcin.Host.Data.Models;
#endif
using Microsoft.AspNetCore.Http;

namespace Burcin.Host.Pages
{
    public partial class ChefPage
    {
		[Inject] public IJSRuntime JsRuntime { get; set; }
		[Inject] public NavigationManager NavigationManager { get; set; }
		public async Task NavigateToUrlAsync(string url, bool openInNewTab)
		{
			if (openInNewTab)
			{
				await JsRuntime.InvokeAsync<object>("open", url, "_blank");
			}
			else
			{
				NavigationManager.NavigateTo(url);
			}
		}

		[Inject] public IHttpContextAccessor HttpContextAccessor { get; set; }
		protected async Task<string> GetCloudFileServiceAsync(string fileName)
		{
			// TODO get it from appsettings
			string bucket = "MyBucket";
			string rest = $"api/CloudStorage/GetFileMetadata?bucketName={bucket}&fileName={fileName}";

			HttpContext context = HttpContextAccessor.HttpContext;
			Uri url = new Uri($"{context.Request.Scheme}://{context.Request.Host}/{rest}");
			using (var client = new HttpClient())
			{
				var response = await client.GetAsync(url).ConfigureAwait(false);
				if (response.IsSuccessStatusCode)
				{
					var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					string signedUrl = null;
					using (var doc = JsonDocument.Parse(jsonString))
					{
						if (doc.RootElement.TryGetProperty("signedUrl", out JsonElement value))
						{
							signedUrl = value.GetString();
						}
					}

					if (signedUrl == null)
					{
						throw new Exception("SignedUrl is `null`");
					}
					await NavigateToUrlAsync(signedUrl, true).ConfigureAwait(false);
				}

				return string.Empty;
			}
		}
		protected string GetTruncatedName(string fileName)
		{
			string output = $"{( fileName.Length > 20 ? fileName.Substring(0, 20) + "..." : fileName )}";
			return output;
		}

        [Inject] ChefService MyService { get; set; }

        private int _page = 1;
        private int _pageSize = 10;

        protected ODataPagedResponse<Chef> Chefs;

        protected List<Chef> Items { get; private set; }

        protected override async Task OnInitializedAsync()
        {
            GetData();
            await base.OnInitializedAsync().ConfigureAwait(false);
        }

        protected void PagerPageChanged(int page)
        {
            _page = page;
            GetData();
            StateHasChanged();
        }

        protected void PagerPageSizeChanged(int pageSize)
        {
            _pageSize = pageSize;
            GetData();
            StateHasChanged();
        }

        private void GetData()
        {
            Chefs = MyService.GetChefsAsync(_page, _pageSize).Result;
            Items = Chefs.Value.ToList();
        }

        protected string ModalTitle { get; set; }
        protected bool ShowModal = false;
        protected Dictionary<string, string> Payload = new Dictionary<string,string>();
        protected void ShowDetails(Chef context)
        {
            ShowModal = true;
            ModalTitle = "Details";
            Payload.Add("Id", context.Id.ToString());
            Payload.Add("Created At", context.CreatedAt.ToString("s"));
        }

        protected void CloseModal()
        {
            ShowModal = false;
            Payload = new Dictionary<string, string>();

        }
    }
}
