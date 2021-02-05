using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class Extension
    {
        [JsonPropertyName("url")]
        public string url { get; set; }

        [JsonPropertyName("extension")]
        public List<Extension2> extension { get; set; } = new List<Extension2>();
    }
}
