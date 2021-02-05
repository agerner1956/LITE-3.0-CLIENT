using Lite.Core.Enums;
using Lite.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Lite.Core.Connections
{
    public class LITEConnection : HttpConnection
    {
        [NonSerialized()]
        public ObservableCollection<RoutedItem> toEGS = new ObservableCollection<RoutedItem>();

        [NonSerialized()]
        public ObservableCollection<RoutedItem> fromEGS = new ObservableCollection<RoutedItem>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("shareDestinations")]
        public List<ShareDestinations> shareDestinations;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("egs")]
        public bool egs;

        //[NonSerialized()]
        //public int loginAttempts = 0;

        [JsonPropertyName("calcCompressionStats")]
        public bool calcCompressionStats { get; set; } = false;

        [JsonPropertyName("minEGSBatchSize")]
        public int minEGSBatchSize = 10240000;

        [JsonPropertyName("resourcePath")]
        public string resourcePath = "/tmp/resourcePath";

        [JsonPropertyName("protocol")]
        public Protocol protocol = Protocol.Http;

        [JsonPropertyName("maxDownloadViaHttpTasks")]
        public int maxDownloadViaHttpTasks = 5;

        [JsonPropertyName("maxStoreTasks")]
        public int maxStoreTasks = 5;

        [JsonPropertyName("boxes")]
        public List<ShareDestinations> boxes;

        [NonSerialized()]
        public int loginAttempts = 0;

        public LITEConnection()
        {
            boxes = new List<ShareDestinations>();
            connType = ConnectionType.lite;
        }
    }
}
