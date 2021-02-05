using Lite.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Lite.Core.Models
{
    /// <summary>
    /// <para>
    /// RoutedItem encompasses all the context objects passed between Connections and other processing actors.
    /// Because this is a transaction context object, the state of the objects and contents can change rapidly.
    /// For example, an inbound DICOM may have tag values modified or added along the way, so at the beginning the tag
    /// values will be what was received from the sender, and at the end could be something completely different
    /// after script processing.  Likewise we can modify the toConnection list arbitrarily via script processing.
    /// This extreme flexibility may cause simple sites grief but is absolutely needed for sophisticated sites.
    /// In other words, simple sites should try to use declarative rules in the profile so it is easy to understand,
    /// and if the declarative rules are insufficient for a sophisticated site then scripts should be used to offer more dynamism.
    /// </para>
    /// </summary>
    public class RoutedItem
    {
        public enum PropertyNames { InstanceID, type, error, priority, fromConnection, status, name, creationTimeUtc, lastWriteTimeUtc, lastAccessTimeUtc, length, taskID, Study, Series, Sop, PatientID, PatientIDIssuer, AccessionNumber };

        public enum Type { UNDEFINED, DICOM, HL7, FILE, RPC, COMPLETION }

        public Type type;

        public enum Status { NEW, PENDING, COMPLETED, FAILED };
        public Guid InstanceID { get; set; }

        public string Error { get; set; }

        public Priority priority = Priority.Low;

        public string fromConnection;
        public Status status;
        public string name;

        public DateTime creationTimeUtc;
        public DateTime lastWriteTimeUtc;

        public DateTime lastAccessTimeUtc;
        public long length;

        public List<KeyValuePair<string, List<string>>> hl7 = new List<KeyValuePair<string, List<string>>>();
        public Dictionary<string, string> TagData = new Dictionary<string, string>();

        public List<ConnectionSet> toConnections = new List<ConnectionSet>();

        /// <summary>
        /// ruleDicomTags contains the list of RuleDicomTag that matches this RoutedItem.
        /// Note to Developer:  Investigate whether transactions reaching this point would have anything other 
        /// than the list RuleDicomTag required to send to the toConnections.         
        /// </summary>
        [NonSerialized()]       
        public List<Tag> ruleDicomTags;

        /// <summary>
        /// ruleDicomTag is populated to provide context to a tag script during execution.  The tag script will use this  property to know which tag on which it should operate.
        /// </summary>
        [NonSerialized()]
        public Tag ruleDicomTag;

        [NonSerialized()]
        public Rules rules;

        // todo: use it
        //[NonSerialized()]
        //public DicomFile sourceDicomFile;

        //[NonSerialized()]
        //public DicomFile destDicomFile;

        //[NonSerialized()]

        //public DicomRequest dicomRequest;

        [NonSerialized()]
        public Stream stream;

        public string sourceFileName;

        public string sourceFileType;

        public string destFileName;

        public string destFileType;

        public int TaskID { get; set; }

        [NonSerialized()]
        public string[] args;

        //[NonSerialized()]
        //public static Logger logger = Logger.logger;

        public bool Compress { get; set; } = true;

        public int attempts = 0;

        public DateTime lastAttempt = DateTime.MinValue;

        public int fileIndex = 0;    // Index of file during a download 

        public int fileCount = 0;    // Number of files in download 

        public string RoutedItemMetaFile;  //name of resiliency file used to remove file when work is complete


        //shb flattened from LITE Request framework
        public string id;                                    // Id from db record
        public int MessageId;                               // Id from dicom
        public DateTime startTime;
        public DateTime? resultsTime;
        public string request;
        public string requestType;
        public List<string> response = new List<string>();  //response
        public List<CloudTaskResults> cloudTaskResults = new List<CloudTaskResults>();

        //the sharingDestination and resource strings are used to build the url to download from EGS, can be used for other things as needed
        public string box;
        public string resource;

        //address oriented routing
        public string from;
        public string to;

        public string StudyID;
        public string Study;
        public string Series;
        public string Sop;
        public string PatientID;
        public string PatientIDIssuer;

        public string AccessionNumber;

        [NonSerialized()]
        public List<Match> matches = new List<Match>();
        public bool RuleMatch = false;

        public RoutedItem()
        {
            this.InstanceID = Guid.NewGuid();
        }

        public RoutedItem(string fromConnection, string id, string request, string requestType)
        {
            this.InstanceID = Guid.NewGuid();
            this.fromConnection = fromConnection;
            this.id = id;
            this.request = request;
            this.requestType = requestType;
            this.startTime = DateTime.Now;
        }

        public RoutedItem(string fromConnection, string sourceFileName, int taskID)
        {
            this.InstanceID = Guid.NewGuid();
            this.fromConnection = fromConnection;
            this.sourceFileName = sourceFileName;
            this.TaskID = taskID;
            this.startTime = DateTime.Now;
        }

        public RoutedItem(string fromConnection, string sourceFileName, int fileIndex, int fileCount, int taskID)
        {
            this.InstanceID = Guid.NewGuid();
            this.fromConnection = fromConnection;
            this.sourceFileName = sourceFileName;
            this.TaskID = taskID;
            this.fileIndex = fileIndex;
            this.fileCount = fileCount;
            this.startTime = DateTime.Now;
        }
    }
}
