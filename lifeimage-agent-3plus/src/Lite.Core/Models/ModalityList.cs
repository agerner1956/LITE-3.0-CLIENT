using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class ModalityList
    {
        [JsonPropertyName("system")]
        public string system { get; set; }

        [JsonPropertyName("code")]
        public string code { get; set; }

        [JsonPropertyName("display")]
        public string display { get; set; }
    }
}
