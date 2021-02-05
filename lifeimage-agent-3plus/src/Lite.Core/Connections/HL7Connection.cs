using Lite.Core.Enums;
using Lite.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Lite.Core.Connections
{
    public partial class HL7Connection : Connection
    {
        [NonSerialized()]
        public ObservableCollection<RoutedItem> ToHL7 = new ObservableCollection<RoutedItem>();

        /// <summary>
        /// ReceiveBufferSize sets the size of the buffer used in communications.  
        /// This is a tunable parameter that will increase or decrease the number of loops the application needs to 
        /// pull data off the tcp stack.Use with caution, numbers too large will leap into the LOH(large object heap)
        /// </summary>        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("receiveBufferSize")]
        public int receiveBufferSize { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("exclusiveAddressUse")]
        public bool exclusiveAddressUse { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("linger")]
        public bool linger { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("lingerms")]
        public int lingerms { get; set; }

        /// <summary>
        ///  maxInboundConnections limits how many sockets are allowed to be active at any one time. This helps to keep the agent and machine responsive and able to handle a DoS event.
        /// </summary>
        [JsonPropertyName("maxInboundConnections")]
        public int maxInboundConnections { get; set; } = 30;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

        [JsonPropertyName("noDelay")]
        public bool noDelay { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("receiveTimeout")]
        public int receiveTimeout { get; set; }

        /// <summary>
        /// sendBufferSize sets the size of the buffer used in communications.  This is a tunable parameter
        /// that will increase or decrease the number of loops the application needs to pull data off the
        /// tcp stack.Use with caution, numbers too large will leap into the LOH(large object heap)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("sendBufferSize")]
        public int sendBufferSize { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("sendTimeout")]
        public int sendTimeout { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("ackMode")]
        public AckMode ackMode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("ack")]
        public string ack { get; set; }

        [JsonPropertyName("IdleTimeout")]
        public int IdleTimeout { get; set; } = 500;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("maxHL7InboundTasks")]
        public int maxHL7InboundTasks { get; set; } = 1;

        public HL7Connection()
        {
            connType = ConnectionType.hl7;

            // todo: move to logic
            //ToHL7.CollectionChanged += ToHL7CollectionChanged;
        }
    }
}
