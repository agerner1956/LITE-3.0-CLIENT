using System;

namespace Lite.Core.Models
{
    public class Series
    {
        public int number { get; set; }

        [NonSerialized]
        public Modality modality;
        public string uid { get; set; }
        public string availability { get; set; }

        [NonSerialized]
        public string description;

        [NonSerialized]
        public BodySite bodySite;
        public DateTime downloadStarted { get; set; }
        public DateTime downloadCompleted { get; set; }
        public int attempts { get; set; }

        //[NonSerialized]
        //public DicomFile dicomFile;

        [NonSerialized]
        public string uri;

        [NonSerialized]
        public string filepath;

    }

    public class BodySite
    {
        public string code { get; set; }
    }

    public class Modality
    {
        public string system { get; set; }
        public string code { get; set; }
        public string display { get; set; }
    }
}
