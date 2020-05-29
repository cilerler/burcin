using System;
using Microsoft.AspNetCore.Components;

namespace Burcin.Host.Shared
{
    public partial class Pager
    {
        [Parameter] public Action<int> PageChanged { get; set; }
        [Parameter] public Action<int> PageSizeChanged { get; set; }
        [Parameter] public long RecordCount { get; set; }
        [Parameter] public int RowCount { get; set; }
        [Parameter] public int CurrentPage { get; set; } = 1;
        [Parameter] public int PageSize { get; set; }

        protected int FirstRowOnPage => (CurrentPage - 1) * PageSize + 1;
        protected int LastRowOnPage => FirstRowOnPage + RowCount - 1;
        protected int PageButtonCount { get; set; } = 5;
        protected int StartPage { get; private set; }
        protected int PreviousPage { get; private set; }
        protected int NextPage { get; private set; }
        protected int FinishPage { get; private set; }

        public int PageCount
        {
            get
            {
                if (RecordCount <= 0 || PageSize <= 0) return 0;

                var pageCount = (double)RecordCount / PageSize;
                return (int)Math.Ceiling(pageCount);
            }
        }

        protected override void OnParametersSet()
        {
            bool lastInSeries = CurrentPage % PageButtonCount == 0;
            int numberOfPages = CurrentPage / PageButtonCount;
            if (lastInSeries)
            {
                numberOfPages -= 1;
            }
            StartPage = Math.Max(numberOfPages * PageButtonCount + 1, 1);
            PreviousPage = Math.Max(StartPage - PageButtonCount, 1);
            FinishPage = Math.Min(StartPage + PageButtonCount - 1, PageCount);
            NextPage = Math.Min(FinishPage + 1, PageCount);

            base.OnParametersSet();
        }

        protected void PagerButtonClicked(int page)
        {
            PageChanged?.Invoke(page);
        }

        protected void PagerPageSizeChanged(int value)
        {
            PageSizeChanged?.Invoke(value);
        }

        protected void DoStuff(ChangeEventArgs e)
        {
            var value = int.Parse(e.Value.ToString());
            PagerPageSizeChanged(value);
        }
    }
}
