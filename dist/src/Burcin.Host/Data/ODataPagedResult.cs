namespace Burcin.Host.Data
{
    public class ODataPagedResponse<T> : ODataResponse<T> where T : class
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public int CurrentPage { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int PageSize { get; set; }
    }
}
