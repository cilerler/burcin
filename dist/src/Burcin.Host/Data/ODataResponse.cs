using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Burcin.Host.Data
{
    public class ODataResponse<T> where T : class
    {
        [JsonPropertyName("@odata.context")]
        public string Context { get; set; }

        [JsonPropertyName("@odata.count")]
        public long RecordCount { get; set; }

        [JsonPropertyName("@odata.nextLink")]
        public string NextLink { get; set; }

        [JsonPropertyName("value")]
        public IList<T> Value { get; set; }
    }
}
