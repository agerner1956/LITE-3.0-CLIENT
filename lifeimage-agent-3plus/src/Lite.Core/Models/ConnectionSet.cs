using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class ConnectionSet
    {
        public ConnectionSet()
        {
            shareDestinations = new List<ShareDestinations>();
        }

        [JsonPropertyName("connectionName")]
        public string connectionName { get; set; }

        [JsonPropertyName("shareDestinations")]
        public List<ShareDestinations> shareDestinations { get; set; }

        public override string ToString()
        {
            return connectionName;
        }
    }
}
