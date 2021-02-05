using Lite.Core.Enums;
using Lite.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading;

namespace Lite.Core.Connections
{
    public partial class LifeImageCloudConnection : HttpConnection
    {
        [JsonPropertyName("CalcCompressionStats")]
        public bool CalcCompressionStats { get; set; } = false;

        /// <summary>
        /// maxStudyDownloadTasks controls how many studies will initiate download in parallel.
        /// </summary>
        [JsonPropertyName("maxStudyDownloadTasks")]
        public int maxStudyDownloadTasks { get; set; } = 5;

        [JsonPropertyName("maxHL7UploadTasks")]
        public int maxHL7UploadTasks { get; set; } = 1;

        [JsonPropertyName("maxPostResponseTasks")]
        public int maxPostResponseTasks { get; set; } = 1;

        /// <summary>
        /// maxWadoTasks controls how many wado tasks can run in parallel for this connection.  NOTE: This is regardless of the maxStudyDownloadTasks parameter.
        /// </summary>
        [JsonPropertyName("maxWadoTasks")]
        public int maxWadoTasks { get; set; } = 5;

        [NonSerialized()]
        public int loginAttempts = 0;

        [JsonPropertyName("maxStowTasks")]
        public int maxStowTasks { get; set; } = 5;

        [JsonPropertyName("organizationCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]        
        public string organizationCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("serviceName")]
        public string serviceName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("subscriptionCode")]
        public string subscriptionCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("tenantID")]
        public string tenantID { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("studySeriesSOPUids")]
        public List<string> studySeriesSOPUids { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("shareDestinations")]
        public List<ShareDestinations> shareDestinations;

        public List<ShareDestinations> Boxes = new List<ShareDestinations>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [NonSerialized()]
        public RootObject studies = new RootObject();

        [NonSerialized()] public string syncToken;

        [NonSerialized()] public ObservableCollection<RoutedItem> toCloud = new ObservableCollection<RoutedItem>();

        [NonSerialized()]
        public ObservableCollection<string[]> markDownloadsComplete = new ObservableCollection<string[]>();

        [NonSerialized()]
        public ObservableCollection<Series> markSeriesComplete = new ObservableCollection<Series>();

        /// <summary>
        /// Group files of at least this size total for a multipart stow.
        /// </summary>
        public int minStowBatchSize { get; set; } = 10240000;

        /// <summary>
        /// LITE Request framework, aka Cloud Agent Task in Cloud code, is async and some connections do not always respond back indicating 
        /// completion.maxRequestAgeMinutes is used to compare the response cache start time, 
        /// populated by LITE Connections immediately prior to issuing the request onto the connection. 
        /// If the age is longer than maxRequestAgeMinutes then the request is marked as complete and the request 
        /// is routed back to the issuer for closure.
        /// </summary>
        public int MaxRequestAgeMinutes { get; set; } = 30;

        public int StudyCloseInterval { get; set; } = 5;

        public LifeImageCloudConnection()
        {
            connType = ConnectionType.cloud;
            InitClass();

            // todo: move to logic
            //toCloud.CollectionChanged += ToCloudCollectionChanged;
            //markDownloadsComplete.CollectionChanged += MarkDownloadsCompleteCollectionChanged;
            //markSeriesComplete.CollectionChanged += MarkSeriesCompleteCollectionChanged;
        }

        /// <summary>
        /// Allows initialization of task processing.
        /// </summary>
        partial void InitClass();
    }
}
