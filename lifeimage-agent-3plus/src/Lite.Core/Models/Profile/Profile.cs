using Lite.Core.Connections;
using Lite.Core.Json;
using Lite.Core.Models;
using Lite.Core.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lite.Core
{
    /// <summary>
    /// Contains Profile class.
    /// </summary>
    public sealed class Profile
    {
        /// <summary>
        /// JsonSchemaPath supplies the location of the json schema used to validate the profile json.
        /// </summary>
        public static string jsonSchemaPath { get; }

        /// <summary>
        /// ModifiedDate indicates when the profile was last read in and to return any profile after this date.  Start with nothing. Needed for loading from a local disk
        /// </summary>
        public static DateTime? modifiedDate;

        /// <summary>
        /// Overrides modifiedDate and rowVersion to always force a reload
        /// </summary>
        public static bool _overrideVersionAndModifiedDate = false;

        /// <summary>
        /// rowVersion field of the profile record stored in the database, returned from the get agent call.  Start with null to get server profile.
        /// </summary>
        public static string rowVersion = null;

        static Profile()
        {
            jsonSchemaPath = "JSONSchema" + Path.DirectorySeparatorChar + "Profile.schema.json";
        }

        public Profile()
        {
            connections = new List<Connection>();
            errors = new List<string>();
            Labels = new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// The name of the profile. Overwritten as file and cloud profiles merge.
        /// </summary>
        [JsonPropertyName("name")]
        public string name { get; set; } = "Bootstrap";

        /// <summary>
        /// Connections is a list of Connection.  Connection is the base class for all connectivity modules
        /// which includes file, LifeImage Cloud, DICOM (DICOM), HL7, etc. Extending this class allows the agent to
        /// work with future connection modules.
        /// </summary>
        [JsonPropertyName("connections")]
        public List<Connection> connections { get; set; }

        /// <summary>
        /// The Labels for the profile. Structure is Key / Localization / String
        /// </summary>
        [JsonPropertyName("Labels")]
        public Dictionary<string, Dictionary<string, string>> Labels { get; set; }

        /// <summary>
        /// ActivationTime allows a future application of a profile change.If the activationTime is greater than now
        /// then the profile merge will be ignored.Used for ITIL change control.    
        /// </summary>
        [JsonPropertyName("activationTime")]
        [JsonPropertyOrder(-5)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]        
        public DateTime activationTime { get; set; }

        /// <summary>
        /// AvailableCodeVersions provides the LITE versions available for upgrade/downgrade
        /// </summary>
        [JsonPropertyName("availableCodeVersions")]
        [JsonPropertyOrder(-10)]
        public string[] availableCodeVersions { get; set; }

        /// <summary>
        /// If dcmtkLibPath is populated, dcmtkConnections will use the path specified.  If not, env path is used.
        /// </summary>
        [JsonPropertyName("dcmtkLibPath")]       
        public string dcmtkLibPath { get; set; }

        ///<summary>
        /// backlogDetection observes the items in toRules for each connection and if not empty, Uses the backlogInterval
        /// instead of the kickoffInterval.This allows for more responsives at the expense of a higher rate of
        /// cloud polling calls.
        ///</summary>        
        [JsonPropertyName("backlogDetection")]
        public bool backlogDetection = true;

        /// <summary>
        /// backlogInterval is the frequency of kickoff during backlog conditions.
        /// </summary>
        [JsonPropertyName("backlogInterval")]
        public int backlogInterval = 5000;        

        [JsonPropertyName("duplicatesDetectionUpload")]
        public bool duplicatesDetectionUpload { get; set; }

        [JsonPropertyName("duplicatesDetectionDownload")]
        public bool duplicatesDetectionDownload { get; set; } = false;

        [JsonPropertyName("duplicatesDetectionInterval")]
        public int duplicatesDetectionInterval { get; set; } = 600;

        [JsonPropertyName("modalityDetectionArchivePeriod")]
        public int modalityDetectionArchivePeriod { get; set; } = 10;

        [JsonPropertyName("modalityList")]
        public List<string> modalityList = new List<string>();

        [JsonPropertyName("connectionEvaluation")]
        public bool connectionEvaluation { get; set; } = true;

        [NonSerialized]
        public bool backlog = true;

        /// <summary>
        /// List of errors from validating the profile.  This will contain errors from both the schema validation and a manual validation for things like missing connections
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("errors")]
        public List<string> errors { get; set; } = new List<string>();

        /// <summary>
        /// highWait is a condition that is triggered when DICOM with tag (0000,0700) is set to HIGH = 0001H.
        /// </summary>
        [NonSerialized]
        public bool highWait = false;

        /// <summary>
        /// highWaitDelay is triggered for all non-HIGH priority work during highWait conditions.
        /// </summary>
        [JsonPropertyName("highWaitDelay")]
        public int highWaitDelay = 60000;

        /// <summary>
        /// JsonInError indicates the json received that is in error. 
        /// The json retrieved from the server or a local file that could not be parsed. 
        /// Valid json with logical errors will not fill in this field.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("jsonInError")]
        public string jsonInError { get; set; } = null;

        /// <summary>
        /// kickoffInterval is the periodic programming loop that ensures all connections are running and other
        /// background tasks are invoked on a schedule.Connections may use this interval or define their own more granular intervals.
        /// </summary>
        [JsonPropertyName("KickOffInterval")]
        public int KickOffInterval = 58000;

        /// <summary>
        /// lastKickOff provides a timestamp of the global kickoff which can be used as a health heartbeat.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("lastKickOff")]
        public DateTime lastKickOff { get; set; }

        /// <summary>
        /// lastStartup indicates the last time LITE was started and can be used to determine run length or perhaps a service interruption.
        /// </summary>
        [JsonPropertyName("lastStartup")]
        public DateTime lastStartup { get; set; } = DateTime.Now;

        /// <summary>
        /// logger is the pluggable logging facility based upon Microsoft.Extensions.Logging.
        /// See https://msdn.microsoft.com/en-us/magazine/mt694089.aspx for info on how to configure your favorite logger.
        /// </summary>
        [JsonPropertyName("logger")]
        public Logger logger { get; set; }

        /// <summary>
        /// LogRetentionDays determines how long to retain the log files.
        /// </summary>
        [JsonPropertyName("logRetentionDays")]
        public int logRetentionDays = 15;

        [JsonPropertyName("LogFileSize")]
        public int LogFileSize = 10240000;

        /// <summary>
        /// maxTaskDuration defines how long a task can run before being terminated.
        /// </summary>
        public TimeSpan maxTaskDuration { get; set; } = new TimeSpan(hours: 1, minutes: 00, seconds: 0);

        /// <summary>
        /// MediumWait is a condition that is triggered when DICOM with tag (0000,0700) is set to MEDIUM = 0000H.
        /// </summary>
        [NonSerialized]
        public bool mediumWait = false;

        /// <summary>
        /// mediumWaitDelay is triggered for all DICOM with tag (0000,0700) set to null and LOW = 0002H priority during highWait conditions.
        /// </summary>
        [JsonPropertyName("mediumWaitDelay")]
        public int mediumWaitDelay { get; set; } = 30000;

        /// <summary>
        /// minFreeDiskBytes controls operation of the agent.  To prevent accidental file corruption, ensure minFreeDiskBytes
        /// is sized appropriately for your workload and rate of acquisition.For example, if your anticipated traffic
        /// can run down the disk faster than the free disk check interval (kickOffInterval), then increase the minFreeDiskBytes
        /// to ensure the disk cannot run out before the next free disk check interval.
        /// </summary>
        [JsonPropertyName("minFreeDiskBytes")]
        public long minFreeDiskBytes { get; set; } = 209715200;

        /// <summary>
        /// Overrides modifiedDate and rowVersion to always force a reload. Used to set _overrideVersionAndModifiedDate instance variable remotely
        /// </summary>
        [JsonPropertyName("overrideVersionAndModifiedDate")]
        internal bool overrideVersionAndModifiedDate { get; set; } = false;

        /// <summary>
        /// profileConverter is the custom JsonConverter used to serialize/deserialize the profile to json.
        /// </summary>
        [Obsolete]
        public static JsonConverter profileConverter { get; set; } = new ProfileConverter();

        /// <summary>
        /// recoveryInterval determines the amount of delay between attempting to reinitialize the agent after a critical error. 
        /// An example of a critical error is if the agent is unable to communicate with the primary liCloud account.
        /// </summary>
        [JsonPropertyName("recoveryInterval")]
        public int recoveryInterval { get; set; } = 10000;

        /// <summary>
        /// The place where rules go.  Rules govern how Connections interact.  In between each Connection must
        /// exist at least one rule, that when evaluated must return true in order for traffic to pass
        /// between the Connections.Rules also contain scripts.Scripts can execute to modify DICOM tags and
        /// other data elements prior to transmission as well as signaling other systems after transmission.
        /// For example, we could run a study through AI image recognition routines and augment studies with image layers and dicom tags of information en route to a destination.  We could notify users with APNS
        /// notifications to their mobile device both before after transmission depending on the use case.  We
        /// could call an enterprise rule engine for instructions on how to route inbound data such as hl7 and
        /// DICOM studies and FHIR calls.We could look up the MPI for an MRN with a call to XDS.For more
        /// information about scripts, see Script.
        /// </summary>
        [JsonPropertyName("rules")]
        public Rules rules = new Rules();

        /// <summary>
        /// run contains a list of runnable commands housed as CommandSets. Examples are commands to stop/restart, download updates, 
        /// view a list of files with ls(new CommandSet("ls", "~"), etc. If the agent is running with it's own identity and limited access this poses very little risk. 
        /// But if the agent is running as root or with elevated privileges in an open insecure network, this list needs
        /// to be filtered for acceptable known commands to prevent running anything.The goal here is provide capabilities
        /// for remote management.We could build a runFilter to contain a list of commands allowed to run for those
        /// seeking to tighten up control, but this is usually best solved at the operating system and network LogLevel.
        /// </summary>
        [JsonPropertyName("run")]
        public List<CommandSet> run = new List<CommandSet>();//new CommandSet("ls", "~")

        /// <summary>
        /// runningCodeVersion is auto-populated by the software so you know for sure which version is running.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("runningCodeVersion")]
        public string runningCodeVersion { get; set; }

        /// <summary>
        /// startupConfigFilePath controls the config file that is read on initial startup.
        /// </summary>
        //public static string startupConfigFilePath = Util.GetTempFolder() + Path.DirectorySeparatorChar + "Profiles" + Path.DirectorySeparatorChar + "startup.config.json";

        /// <summary>
        /// startupParams is the instance of the data read from the startup.config.json file
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public StartupParams startupParams { get; set; } = new StartupParams();

        /// <summary>
        /// taskDelay is used when a task needs to wait for something, like when it is throttled, before it tries again.
        /// </summary>
        [JsonPropertyName("taskDelay")]
        public int taskDelay { get; set; } = 1000;

        /// <summary>
        /// tempPath is the location where LITE stores temporary data and must NOT include a trailing slash or backslash.
        /// </summary>
        [JsonPropertyName("tempPath")]
        public string tempPath { get; set; }

        /// <summary>
        /// tempFileRetentionHours determines how long to retain the temp files.
        /// </summary>
        [JsonPropertyName("tempFileRetentionHours")]
        public int tempFileRetentionHours = 4;

        /// <summary>
        /// updateCodeVersion is used to trigger a download and replacement of runningCodeVersion with updateCodeVersion.
        /// If updateCodeVersion is null or equal to runningCodeVersion, nothing happens.
        /// This can be used in combination with activationTime to trigger a scheduled version update.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("updateCodeVersion")]
        public string updateCodeVersion { get; set; }

        /// <summary>
        /// updateUrl used to retrieve agent update.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("updateUrl")]
        public string updateUrl { get; set; }

        /// <summary>
        /// updatePassword is the encrypted password used to retrieve agent update
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        //[JsonIgnore]
        public CryptoField updatePassword { get; set; } = new CryptoField();

        /// <summary>
        /// username used to retrieve agent update
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("updateUsername")]
        public string updateUsername { get; set; }

        [JsonPropertyName("UsePowershellUpgrade")]
        public bool UsePowershellUpgrade { get; set; } = false;

        /// <summary>
        /// useSocketsHttpHandler, new in .net core 2.1, is used if there is a need to revert to.net core 2.0 socket behavior. If this is the case set the value to false.
        /// </summary>
        [JsonPropertyName("useSocketsHttpHandler")]
        public bool useSocketsHttpHandler { get; set; } = true;

        /// <summary>                  
        /// version is the profile indicator for determining how to handle inbound profiles that are different 
        /// than the one currently in memory.Inbound profiles can be on disk or coming from LifeImageCloud.Lower inbound versions are completely ignored.Same inbound versions are checked for new connections and rules and these are added on the fly.Higher inbound versions cause LITE to shut down existing work in progress and perform a complete 
        /// re-initialization. 
        /// +1 this along with your changes when you want to completely override existing settings. 
        /// LifeImageCloud UI automatically increments the version when clicking "Save profile".
        /// </summary>
        [JsonPropertyName("version")]
        public int version = -1;

        /// <summary>
        /// IsInError returns true if the profile in error
        /// </summary>
        /// <returns></returns>
        public bool IsInError()
        {
            return jsonInError != null;
        }

        public string GetUnprotectedUpdatePassword()
        {
            var temp = updatePassword.GetUnprotectedField();
            return temp;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
