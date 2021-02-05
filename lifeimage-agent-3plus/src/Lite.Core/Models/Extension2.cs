using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class Extension2
    {
        [JsonPropertyName("url")]
        public string url { get; set; }

        [JsonPropertyName("valueString")]
        public string valueString { get; set; }

        [JsonPropertyName("valueCode")]
        public string valueCode { get; set; }

        [JsonPropertyName("valueDate")]
        public string valueDate { get; set; }
    }
}
