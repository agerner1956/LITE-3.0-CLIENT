using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;

namespace Lite.Services
{
    public sealed class ProfileMerger : IProfileMerger
    {
        private readonly IUtil _util;
        private readonly ILogger _logger;
        private readonly IProfileWriter _profileWriter;
        private readonly IConnectionFinder _connectionFinder;
        private readonly ILoggerManager _loggerManager;
        private readonly ILITETask _taskManager;

        public ProfileMerger(
            IUtil util,
            IProfileWriter profileWriter,
            IConnectionFinder connectionFinder,
            ILoggerManager loggerManager,
            ILITETask taskManager,
            ILogger<ProfileMerger> logger)
        {
            _util = util;
            _profileWriter = profileWriter;
            _connectionFinder = connectionFinder;
            _loggerManager = loggerManager;
            _taskManager = taskManager;
            _logger = logger;
        }

        /// <summary>
        /// Merges another profile into this one in an additive fashion unless version is higher
        /// This should preserve the arg > file > remote precedence with the exception of variables required to be defined
        /// at program startup like kickoffInterval, logRetentionDays, name, etc.
        /// Command line args boot with version = 0 unless otherwise specified.
        /// To add settings without agent restart, add at Cloud or agent with same version.
        /// To replace settings without agent restart, add at Cloud or agent with incremented version.Note: live queues will reset.
        /// If you switch profiles at the agent, the name will (should) be different and merge is skipped.
        /// </summary>
        /// <param name="current"></param>
        /// <param name="profile"></param>
        public void MergeProfile(Profile current, Profile profile)
        {
            Throw.IfNull(current);

            bool bootstrap = current.name.Equals("Bootstrap");

            if (profile == null)
            {
                return;
            }

            lock (current)
            {
                //game over!! Someone switched profiles on the client or server or it's not time
                if ((!current.name.Equals(profile.name) && !bootstrap) || !(DateTime.Now > profile.activationTime))
                {
                    _logger.Log(LogLevel.Debug, $"inbound profile name: {profile.name} this name: {current.name} bootstrap: {bootstrap}, inbound activationTime: {profile.activationTime} using ignore strategy");
                    return;
                }

                if (profile.version > current.version)
                {
                    _logger.Log(LogLevel.Debug, $"inbound profile version {profile.version} greater than current version {current.version}, using replacement strategy");

                    // foreach (var conn in this.connections)
                    // {
                    //     Logger.logger.Log(TraceEventType.Information, $"stopping {conn.name}");
                    //     conn.stop();
                    // }
                    _taskManager.Stop();

                    //replace all settings, realizing this dumps all live queues and connections
                    //may want code that is more fine grained and doesn't drop work in progress. 
                    current.activationTime = profile.activationTime;
                    current.availableCodeVersions = profile.availableCodeVersions;
                    //this.backlog; shb nonserialized


                    current.backlogDetection = profile.backlogDetection;
                    current.backlogInterval = profile.backlogInterval;

                    /* 2020-05-22 AMG added properties to profile */
                    current.duplicatesDetectionUpload = profile.duplicatesDetectionUpload;
                    current.duplicatesDetectionDownload = profile.duplicatesDetectionDownload;
                    current.duplicatesDetectionInterval = profile.duplicatesDetectionInterval;
                    current.modalityList = profile.modalityList;
                    current.modalityDetectionArchivePeriod = profile.modalityDetectionArchivePeriod;

                    current.connections = profile.connections;
                    current.dcmtkLibPath = profile.dcmtkLibPath;

                    //this.errors = profile.errors; shb not needed
                    //this.highWait = profile.highWait; shb nonserialized
                    current.highWaitDelay = profile.highWaitDelay;
                    //this.jsonInError = profile.jsonInError; shb not needed
                    //this.jsonSchemaPath = profile.jsonSchemaPath; shb not needed
                    current.KickOffInterval = profile.KickOffInterval;
                    //this.lastKickOff = profile.lastKickOff; shb not needed
                    //this.lastStartup = profile.lastStartup; shb not needed
                    current.Labels = profile.Labels;

                    //2018-02-13 shb need to assign inbound profile.logger settings during replacement strategy
                    var primary = _connectionFinder.GetPrimaryLifeImageConnection(current);

                    //current.logger = new Logger("default");
                    current.logger = new Logger();

                    current.logger.ConsoleTraceLevel = profile.logger.ConsoleTraceLevel;
                    current.logger.SplunkTraceLevel = profile.logger.SplunkTraceLevel;
                    current.logger.FileTraceLevel = profile.logger.FileTraceLevel;
                    current.logger.TracePattern = profile.logger.TracePattern;
                    Logger.logger = current.logger;
                    //Logger.logger.Init();
                    _loggerManager.Init(current.logger);

                    current.LogFileSize = profile.LogFileSize;
                    current.logRetentionDays = profile.logRetentionDays;
                    current.maxTaskDuration = profile.maxTaskDuration;
                    //this.mediumWait = profile.mediumWait; shb nonserialized
                    current.mediumWaitDelay = profile.mediumWaitDelay;
                    current.minFreeDiskBytes = profile.minFreeDiskBytes;
                    //this.modifiedDate = profile.modifiedDate; shb not needed
                    current.name = profile.name;
                    Profile._overrideVersionAndModifiedDate = Profile._overrideVersionAndModifiedDate;
                    //profileConverter not needed
                    current.recoveryInterval = profile.recoveryInterval;
                    //this.rowVersion = profile.rowVersion; shb not merged because we get this from the api call
                    current.rules = profile.rules;
                    current.run = profile.run;
                    //this.runningCodeVersion = profile.runningCodeVersion; shb not needed
                    // Only allow startup params in startup profile                        this.startupParams = profile.startupParams;
                    //startupConfigFilePath shb not needed
                    //this.startupParams = profile.startupParams; shb not needed
                    current.taskDelay = profile.taskDelay;
                    current.tempFileRetentionHours = profile.tempFileRetentionHours;
                    current.tempPath = profile.tempPath;
                    current.updateCodeVersion = profile.updateCodeVersion;
                    current.updateUrl = profile.updateUrl;
                    current.updatePassword = profile.updatePassword;
                    current.updateUsername = profile.updateUsername;
                    current.useSocketsHttpHandler = profile.useSocketsHttpHandler;
                    current.version = profile.version;

                    // shb will change the value of tempPath and write back to a profile if saved (including possibly the same profile).
                    //convenience assignment to reduce number of calls to get Windows ProgramData folder.
                    current.tempPath = _util.GetTempFolder(current.tempPath);

                    _profileWriter.SaveProfile(current).Wait();

                    if (!bootstrap)
                    {
                        throw new Exception("Replacement Strategy Needs Full LITE.init(), throwing this exception on purpose!");
                    }
                }
                else if (profile.version == current.version)
                {
                    _logger.Log(LogLevel.Debug, $"inbound profile version {profile.version} same as current version {current.version}, using merge strategy");

                    //2018-03-03 shb need to assign inbound profile.logger settings during merge so we can have non-destructive loglevel changes
                    //                        this.logger.logLevel = profile.logger.logLevel;
                    var primary = _connectionFinder.GetPrimaryLifeImageConnection(current);
                    //current.logger = new Logger("default");
                    current.logger = new Logger();
                    //                        this.logger.logLevel = profile.logger.logLevel;
                    current.logger.ConsoleTraceLevel = profile.logger.ConsoleTraceLevel;
                    current.logger.SplunkTraceLevel = profile.logger.SplunkTraceLevel;
                    current.logger.FileTraceLevel = profile.logger.FileTraceLevel;
                    current.logger.TracePattern = profile.logger.TracePattern;
                    Logger.logger = current.logger;
                    //Logger.logger.Init();
                    _loggerManager.Init(current.logger);

                    if (current.updateCodeVersion == null)
                        current.updateCodeVersion = profile.updateCodeVersion;

                    //merge settings in additive fashion except override ones that startup with predefined values
                    foreach (var srcConn in profile.connections)
                    {
                        Connection destConn = current.connections.Find(e => e.name == srcConn.name);

                        if (destConn == null)
                        {
                            //srcConn.profile = this;
                            current.connections.Add(srcConn);
                        }
                    }

                    //dev hack to load in script example.
                    Script script = new Script
                    {
                        name = "Hello World",
                        source = "using System.Diagnostics; if (logger.logLevel == \"Trace\") logger.Log(TraceEventType.Verbose, $\"Hello World\");"
                    };

                    if (current.rules.scripts.Find(e => e.name == script.name) == null)
                    {
                        _logger.Log(LogLevel.Debug, $"adding script {script.name}");
                        current.rules.scripts.Add(script);
                    }

                    foreach (var rule in profile.rules.destRules)
                    {
                        if (!current.rules.destRules.Exists(e => e.name == rule.name))
                        {
                            current.rules.destRules.Add(rule);
                        }
                    }

                    var msg = "";
                    foreach (var rule in current.rules.destRules)
                    {
                        msg += rule.name + " ";
                    }

                    _logger.Log(LogLevel.Debug, $"{current.rules.destRules.Count} rules after merge: {msg}");
                }
                else if (profile.version < current.version)
                {
                    //ignore
                    _logger.Log(LogLevel.Debug, $"inbound profile version {profile.version} less than current version {current.version}, using ignore strategy");
                    return;
                }
                else
                {
                    //ignore
                    _logger.Log(LogLevel.Debug, $"Unexpected condition inbound version {profile.version} current version {current.version}, using ignore strategy");
                    return;
                }
            }
        }
    }
}