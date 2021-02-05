using System.Text.Json.Serialization;

namespace Lite.Core
{
    /// <summary>
    /// Contains startup information about application.
    /// </summary>
    public sealed class StartupParams
    {
        /// <summary>
        /// Used only in startup configuration, indicates which local profile to load.
        /// </summary>
        [JsonPropertyName("localProfilePath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]        
        public string localProfilePath { get; set; }

        /// <summary>
        /// Used only in startup configuration, indicates which local profile to load.
        /// </summary>
        [JsonPropertyName("saveProfilePath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string saveProfilePath { get; set; }

        /// <summary>
        /// Used only in startup configuration, indicates whether to load profile from server.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("getServerProfile")]
        public bool getServerProfile { get; set; }

        /// <summary>
        /// Used only in startup configuration, indicates whether to save profile to server.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("putServerProfile")]
        public bool putServerProfile { get; set; }

        /// <summary>
        /// Used only in startup configuration, indicates whether to generate a schema.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("generateSchema")]
        public bool generateSchema { get; set; }

        /// <summary>
        /// Used only in startup configuration, indicates whether to generate a schema.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("validateProfile")]
        public bool validateProfile { get; set; }
    }
}
