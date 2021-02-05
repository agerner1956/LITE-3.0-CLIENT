using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class ImagingStudy
    {
        [JsonPropertyName("resourceType")]
        public string resourceType { get; set; }

        [NonSerialized]
        public List<Extension> extension;

        [JsonPropertyName("started")]
        public string started { get; set; }

        [NonSerialized]
        public Patient patient;

        [JsonPropertyName("uid")]
        public string uid { get; set; }

        [NonSerialized]
        public Accession accession;

        [NonSerialized]
        public List<ModalityList> modalityList;

        [JsonPropertyName("availability")]
        public string availability { get; set; }

        [JsonPropertyName("url")]
        public string url { get; set; }

        [JsonPropertyName("numberOfSeries")]
        public int numberOfSeries { get; set; }

        [JsonPropertyName("numberOfInstances")]
        public int numberOfInstances { get; set; }

        [NonSerialized]
        public string description;

        [JsonPropertyName("series")]
        public List<Series> series { get; set; } = new List<Series>();

        [NonSerialized]
        public Referrer referrer;

        [JsonPropertyName("downloadStarted")]
        public DateTime downloadStarted { get; set; }

        [JsonPropertyName("downloadCompleted")]
        public DateTime downloadCompleted { get; set; }

        [JsonPropertyName("attempts")]
        public int attempts { get; set; }
    }
}
