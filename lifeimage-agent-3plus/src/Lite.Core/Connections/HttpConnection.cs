using Lite.Core.Enums;
using System;
using System.Text.Json.Serialization;

namespace Lite.Core.Connections
{
    public abstract class HttpConnection : Connection
    {
        public HttpConnection()
        {
            connType = ConnectionType.http;
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("URL")]
        public string URL { get; set; }

        [NonSerialized()]
        public string jSessionID;

        [NonSerialized()]
        public bool loginNeeded = true;

        public System.TimeSpan httpRequestTimeout = new System.TimeSpan(days: 0, hours: 0, minutes: 30, seconds: 0, milliseconds: 0);

        /// <summary>
        /// maxConnectionsPerServer limits how many outbound sockets are allowed to be active at any one time. This helps to keep the agent from causing a DoS event on a remote resource.
        /// </summary>
        public int maxConnectionsPerServer { get; set; } = 10;
    }
}
