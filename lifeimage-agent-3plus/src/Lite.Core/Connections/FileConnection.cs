using Lite.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;

namespace Lite.Core.Connections
{
    /// <summary>
    /// FileConnection provides the ability to send files and receive objects and serialize them as files.  Examples include 
    /// receiving objects from LifeImage Cloud or a DICOMConnection and storing them in inpath as files, and using scanpath 
    /// to monitor a directory for files to send to another connection.When the file is detected and sent from scanpath,
    /// FileConnection moves the file to outpath.
    /// </summary>
    public sealed class FileConnection : Connection
    {
        /// <summary>
        /// inpath is the path to store inbound files to the FileConnection from other Connections.
        /// </summary>
        //[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("inpath")]
        public string inpath { get; set; }

        /// <summary>
        /// inpathRetentionHours determines how long to retain the temp files.
        /// </summary>
        [JsonPropertyName("inpathRetentionHours")]
        public int inpathRetentionHours = 72;

        /// <summary>
        /// outpath is the path to store files after sending to another Connection. Used in conjunction with scanpath.
        /// </summary>
        /// <remarks>
        /// path to store after sending. 
        /// </remarks>
        //[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("outpath")]
        public string outpath { get; set; } 

        /// <summary>
        /// outpathRetentionHours determines how long to retain the temp files.
        /// </summary>
        [JsonPropertyName("outpathRetentionHours")]
        public int outpathRetentionHours { get; set; } = 72;

        /// <summary>
        /// scanpaths is a list of paths to scan for files to process.  Also accepted is a list of files.
        /// </summary>
        /// <remarks>
        /// Full path files or folders  
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("scanpaths")]
        public List<string> scanpaths { get; set; } = new List<string>();

        [NonSerialized()] 
        SemaphoreSlim ScanPathSignal = new SemaphoreSlim(0, 1);

        public FileConnection()
        {
            connType = ConnectionType.file;
        }
    }
}
