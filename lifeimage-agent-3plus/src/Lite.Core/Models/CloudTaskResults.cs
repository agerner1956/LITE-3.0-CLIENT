using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    /// <summary>
    /// Info sent back to the cloud to indicate the results of a query
    /// </summary>
    public sealed class CloudTaskResults
    {
        public CloudTaskResults()
        {
            results = new List<string>();
        }

        /// <summary>
        /// connection response came from
        /// </summary>
        [JsonPropertyName("connectionName")]
        public string connectionName { get; set; }                          

        [JsonPropertyName("mrn")]
        public string mrn { get; set; }

        [JsonPropertyName("accessionNumber")]
        public string accessionNumber { get; set; }

        [JsonPropertyName("results")]
        public List<string> results { get; set; }       // query can return multiple results
    }
}
