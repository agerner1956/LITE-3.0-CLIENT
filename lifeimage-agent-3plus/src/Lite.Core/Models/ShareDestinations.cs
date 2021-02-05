using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class ShareDestinations
    {
        [JsonPropertyName("boxId")]
        public string boxId;

        [JsonPropertyName("groupId")]
        public string groupId;

        [JsonPropertyName("groupName")]
        public string groupName;

        [JsonPropertyName("publishableBoxType")]
        public string publishableBoxType;

        [JsonPropertyName("organizationName")]
        public string organizationName;

        [JsonPropertyName("boxName")]
        public string boxName;

        /// <summary>
        /// boxUuid may be guid or unique email style address for AOR (Address-oriented routing)
        /// </summary>
        [JsonPropertyName("boxUuid")]
        public string boxUuid;
    }
}
