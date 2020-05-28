using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Burcin.Api.Shared
{
    //public class TableTemplateBase<TItem> : ComponentBase

    public partial class TableTemplate<TItem>
    {
        [Parameter]
        public RenderFragment TableHeader { get; set; }
        [Parameter]
        public RenderFragment<TItem> RowTemplate { get; set; }
        [Parameter]
        public RenderFragment TableFooter { get; set; }
        [Parameter]
        public RenderFragment TableCaption{ get; set; }
        [Parameter]
        public IReadOnlyList<TItem> Items { get; set; }

        //protected override async Task OnInitializedAsync()
        //{
        //}
        //
        //protected void UpdateList()
        //{
        //    StateHasChanged();
        //}
    }
}
