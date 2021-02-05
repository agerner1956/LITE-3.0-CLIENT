using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class Accession
    {
        [JsonPropertyName("value")]
        public string value { get; set; }
    }
}
