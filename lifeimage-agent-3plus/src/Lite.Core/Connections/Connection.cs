using Lite.Core.Enums;
using Lite.Core.IoC;
using Lite.Core.Json;
using Lite.Core.Models;
using Lite.Core.Security;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Lite.Core.Connections
{
    public class Connection
    {
        [NonSerialized()]
        public static Dictionary<string, List<RoutedItem>> cache;

        [NonSerialized()]
        protected static object InitLock = new object();

        static Connection()
        {
            cache = new Dictionary<string, List<RoutedItem>>();
        }

        public string ServicePoint = "localhost";
        public Dictionary<string, bool> stores = new Dictionary<string, bool>();
        public StoreName X509StoreName = StoreName.My;
        public StoreLocation X509StoreLocation = StoreLocation.CurrentUser;

        [NonSerialized()]
        public Dictionary<string, FileSystemWatcher> FileSystemWatchers = new Dictionary<string, FileSystemWatcher>();

        public bool EnforceServerCertificate = false;

        public int ResponseCacheExpiryMinutes { get; set; } = Constants.Connections.ResponseCacheExpiryMinutes;

        /// <summary>
        /// Name of the Connection.         
        /// Setting the order to -2 or lower puts it ahead of all fields that are unassigned which are set to -1
        /// Set to -100 to give derived classes room to insert themselves
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("name")]
        public string name { get; set; }

        /// <summary>
        /// Enabled determines whether the Connection is instantiated, and whether it is a valid receiver of traffic in the event a rule points to this Connection as a destination.
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("enabled")]
        public bool enabled { get; set; } = true;

        public bool requestResponseEnabled = true;

        /// <summary>
        /// ConnType indicates the ConnectionType of this Connection and is used in processing to activate special handling
        /// unique to each type, if any.For example, http connections may need to know when network connectivity is
        /// disrupted.
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("connType")]
        public ConnectionType connType { get; set; }

        /// <summary>
        /// remoteHostname indicates the remote hostname. For details refer to examples in Profiles/ and to the specific Connection type.
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("remoteHostname")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string remoteHostname { get; set; }

        /// <summary>
        /// localHostname indicates the local hostname of LITE.  For details refer to examples in Profiles/ and to the specific Connection type.
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("localHostname")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string localHostname { get; set; }

        public bool UseIPV4 = true;
        public bool UseIPV6 = false;

        /// <summary>
        /// localPort indicates the local port on LITE.  For details refer to examples in Profiles/ and to the specific Connection type.
        /// </summary>
        [JsonPropertyOrder(-90)]
        [JsonPropertyName("localPort")]
        public int localPort { get; set; }

        /// <summary>
        /// remotePort indicates the remote port.  For details refer to examples in Profiles/ and to the specific Connection type.
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("remotePort")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int remotePort { get; set; }

        [JsonPropertyName("sendIntermediateResults")]
        public bool sendIntermediateResults = false;

        [NonSerialized()]
        public bool started;

        [JsonPropertyName("TestConnection")]
        public bool TestConnection = false;

        [NonSerialized()]
        public BlockingCollection<RoutedItem> toRules = new BlockingCollection<RoutedItem>();

        [NonSerialized()]
        public SemaphoreSlim ToRulesSignal = new SemaphoreSlim(0, 1);

        /// <summary>
        /// tls may not be supported on all connections. Refer to the specific connection for details.  
        /// </summary>
        [JsonPropertyName("useTLS")]
        public bool useTLS { get; set; }

        /// <summary>
        /// username is required to connect to a secured resource.
        /// </summary>
        [JsonPropertyName("username")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string username { get; set; }

        /// <summary>
        /// password is the private class storage for Password. Password has special getters and setters to handle encryption.
        /// </summary>
        [JsonPropertyName("password")]
        public string password { get; set; }

        /// <summary>
        /// responsive, if implemented, determines whether the worker loops on this connection are long running and thus responsive to events as they arrive, as compared to only periodic polling on the kickoff interval.
        /// </summary>
        [JsonPropertyName("responsive")]
        public bool responsive { get; set; } = true;

        /**
          max[Something] attributes indicate for this connection instance how many tasks of it's kind can run at a time.
          Also refer to Profile.maxTasks which indicates for the entire agent instance how many total
          tasks can run at a time.  Too many executing tasks will slow performance while not enough will
          also slow performance and affect parallelism.  Tuning maxTasks at the connection level will
          allow configurations such as 5 up/5 down for balanced bi-directional, 9 up/1 down for a primary 
          push site, or 1 up/9 down for a primary pull site.  If a task uses a lot of CPU, then you want to 
          match 1 task per cpu core. If a task spends most of its time waiting on network/server delays, then 
          you can have several tasks per core.  Assigning too many tasks to a connection can also negatively 
          impact the external dicom, hl7 or cloud server.  A standard rule of thumb for http type connections
          is to behave like a browser, which is 2-12 connections max.  DICOM server capability will vary
          widely from desktop dicom like Osirix and Clearcanvas that can not handle much workload due to 
          disk and cpu performance constraints while an enterprise grade DICOM may support ten or more
          connections per client.  Always consult your server admin before deciding on the values of 
          maxTasks and maxConnections to avoid accidental outages due to overload.
         */
        /**
        Password is required to connect to a secured resource.  
        When password is set (command line, loaded from json, read from cloud) it is checked
        to see whether it is secure. If it is not secure, it is secured with AES encryption.
        If loadProfileFile= is the same file as saveProfileFile=, the original cleartext password
        will be over-written with the secured password, along with the sharedKey and IV.
        When password is retrieved, it will decrypt if necessary.
         */
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Password
        {
            get { return password; }
            set
            {
                if (value != null && !value.Equals(""))
                {
                    ICrypto crypto = CryptoStatic.Instance();
                    password = crypto.Protect(value);
                    sharedKey = Convert.ToBase64String(crypto.Key);
                    IV = Convert.ToBase64String(crypto.IV);

                }
                else
                {
                    password = null;
                    sharedKey = null;
                    IV = null;
                }
            }
        }

        protected string GetUnprotectedPassword()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// sharedKey is used to store the encryption key to a secured resource. The specifics are defined by the type of Connection.For example, an liCloud connection saves its password using AES, along with the Key and IV.
        /// </summary>
        [JsonPropertyName("sharedKey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string sharedKey { get; set; }

        /// <summary>
        /// IV, or initialization vector, is used in AES Encryption.  For example, an liCloud connection saves its password using AES along with the Key and IV.
        /// </summary>
        [JsonPropertyName("IV")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string IV { get; set; }

        [JsonPropertyName("maxAttempts")]
        public int maxAttempts { get; set; } = Constants.Connections.maxAttempts;

        [JsonPropertyName("retryDelayMinutes")]
        public int retryDelayMinutes { get; set; } = Constants.Connections.retryDelayMinutes;

        /// <summary>
        /// isPrimary is used to determine which connection of a given type is primary, in the event more than one is defined.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("isPrimary")]
        public bool isPrimary { get; set; }

        /// <summary>
        /// inout determines whether the connection only receives data (in) or is only a resource (out), or both. inbound or outbound connection(for the types that can handle both)
        /// </summary>
        [JsonPropertyName("inout")]
        public InOut inout { get; set; }

        /// <summary>
        /// pushPull determines whether the connection, if it is a resource (outbound), makes  available the resources via push or the resource is available to pull by the client, or both.In LITEConnection, 
        /// for example, resources can be pushed to EGS in the case of a firewalled LITE instance, or EGS can be notified  of the resource and EGS can pull it, 
        /// if the listening port is open to the public.  
        /// If both, the resource is pushed and will remain available for pull if needed by another connection(if supported).  
        /// As of this writing LITEConnection can only be in push or pull mode but not both.
        /// </summary>
        /// <remarks>
        /// inbound or outbound connection (for the types that can handle both)
        /// </remarks>
        [JsonPropertyName("pushPull")]
        public PushPullEnum PushPull { get; set; } = PushPullEnum.push;
    }
}
