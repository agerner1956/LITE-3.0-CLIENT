using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class Referrer
    {
        [JsonPropertyName("display")]
        public string display { get; set; }
    }
}
