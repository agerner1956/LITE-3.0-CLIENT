using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.Utils;
using Lite.Services.Configuration;
using Lite.Services.Connections;
using Lite.Services.Connections.Cloud;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LiteEngine : ILiteEngine
    {
        private readonly IScriptService _scriptService;
        private readonly IProfileManager _profileManager;
        private readonly IProfileWriter _profileWriter;
        private readonly IConnectionManagerFactory _connectionManagerFactory;
        private readonly ICloudProfileLoaderService _cloudProfileLoaderService;
        private readonly IConnectionFinder _connectionFinder;        
        private readonly IProfileStorage _profileStorage;
        private readonly ILitePurgeService _litePurgeService;
        private readonly ILITETask _taskManager;
        private readonly IConfigurationLoader _configurationLoader;
        private readonly IProfileConnectionsInitializer _connectionsInitializer;
        private readonly ILogger _logger;

        public LiteEngine(
            IUtil util,
            IProfileManager profileManager,
            IScriptService scriptService,
            IProfileWriter profileWriter,
            IConnectionManagerFactory connectionManagerFactory,
            ICloudProfileLoaderService cloudProfileLoaderService,
            ILITETask liteTaskManager,
            IProfileStorage profileStorage,
            IConnectionFinder connectionFinder,
            ILitePurgeService litePurgeService,
            IConfigurationLoader configurationLoader,
            IProfileConnectionsInitializer connectionsInitializer,
            ILogger<LiteEngine> logger)
        {
            Throw.IfNull(util);
            _profileManager = profileManager;
            _profileWriter = profileWriter;
            _connectionManagerFactory = connectionManagerFactory;
            _cloudProfileLoaderService = cloudProfileLoaderService;
            _scriptService = scriptService;
            _taskManager = liteTaskManager;
            _profileStorage = profileStorage;
            _connectionFinder = connectionFinder;
            _litePurgeService = litePurgeService;
            _configurationLoader = configurationLoader;
            _connectionsInitializer = connectionsInitializer;
            _logger = logger;
        }

        // todo: move on service level! avoid static!
        private static List<string> argsList = new List<string>();

        // todo: move on service level! avoid static!
        public static bool fastStatus = false;

        // todo: move on service level! avoid static!
        public static int kickOffCount;

        // todo: move on service level! avoid static!
        public static string ProfileLocker = "ProfileLocker";

        // todo: move on service level! avoid static!
        public static int RecoveryCount = 0;

        // todo: move on service level! avoid static!
        public static string version;

        // todo: move on service level! avoid static!
        public static string platform;

        public void Start()
        {
            try
            {
                StartImpl(Array.Empty<string>());
                ShowGreetings();
            }
            catch (Exception e)
            {
                throw new Exception("Could not start LITE engine", e);
            }
        }

        public void Stop()
        {
            _logger.Log(LogLevel.Warning, "Shutdown Requested");
            _logger.Log(LogLevel.Warning, "Stopping Tasks for Shutdown");

            // todo: could use true
            //_liteTaskManager.Stop(true);
            _taskManager.Stop();

            _logger.Log(LogLevel.Warning, "Stopping Connections");
            var profile = _profileStorage.Current;
            if (profile != null && profile.connections != null)
            {
                foreach (Connection conn in profile.connections)
                {
                    var connectionManager = _connectionManagerFactory.GetManager(conn);
                    if (connectionManager != null)
                    {
                        connectionManager.Stop();
                    }
                }
            }

            _logger.Log(LogLevel.Warning, "Stopping Logger");

#if DEBUG

            //Console.WriteLine("Calling Environment Exit");
            //Environment.Exit(0);
#endif
        }

        public void Dispose()
        {
            Stop();
        }

        public async Task Process()
        {
            var taskInfo = $"";
            var profile = _profileStorage.Current;

            _logger.Log(LogLevel.Information, "Life Image Transfer Exchange Loop");

            try
            {
                try
                {
                    lock (ProfileLocker)
                    {
                        _profileManager.LoadProfile(profile, profile.startupParams.localProfilePath);
                        //Profile.LoadProfile(profile, profile.startupParams.localProfilePath);
                        if (profile.duplicatesDetectionUpload)
                        {
                            string message = "Duplicates detection and elimination process is active for upload process.";
                            _logger.Log(LogLevel.Information, $"{message}");
                        }
                        else
                        {
                            string message = "Duplicates detection and elimination process is not active for upload process.";
                            _logger.Log(LogLevel.Information, $"{message}");
                        }
                        if (profile.duplicatesDetectionDownload)
                        {
                            string message = "Duplicates detection and elimination process is active for download process.";
                            _logger.Log(LogLevel.Information, $"{message}");
                        }
                        else
                        {
                            string message = "Duplicates detection and elimination process is not active for download process.";
                            _logger.Log(LogLevel.Information, $"{message}");
                        }
                        //var primaryConnection = profile.GetPrimaryLifeImageConnection();
                        var primaryConnection = _connectionFinder.GetPrimaryLifeImageConnection(profile);
                        if (primaryConnection == null)
                        {
                            throw new Exception($"Primary Connection Missing out of {profile.connections.Capacity} total connections.");
                        }

                        if (platform.Equals("win") && string.IsNullOrEmpty(profile.dcmtkLibPath))
                        {
                            Console.WriteLine(profile.name);
                            profile.dcmtkLibPath = "tools" + Path.DirectorySeparatorChar + Constants.Dirs.dcmtk + Path.DirectorySeparatorChar + "dcmtk-3.6.3-win64-dynamic";
                            //profile.SaveProfile();
                            _profileWriter.SaveProfile(profile).Wait();
                        }

                        if (!primaryConnection.loginNeeded)
                        {
                            _cloudProfileLoaderService.LoadProfile(profile, primaryConnection).Wait();
                            //profile.LoadProfile(primaryConnection).Wait();
                        }

                        //profile.SaveProfile();
                        _profileWriter.SaveProfile(profile).Wait();

                        bool isEnabled = false;
                        AppContext.TryGetSwitch("System.Net.Http.useSocketsHttpHandler", out isEnabled);

                        if (isEnabled != profile.useSocketsHttpHandler)
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Calling AppContext.SetSwitch(\"System.Net.Http.useSocketsHttpHandler\", {profile.useSocketsHttpHandler})"); AppContext.SetSwitch("System.Net.Http.useSocketsHttpHandler", profile.useSocketsHttpHandler);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("Replacement Strategy Needs Full LITE.init()"))
                    {
                        _logger.Log(LogLevel.Critical, $"{taskInfo} Configuration changed received.");
                    }
                    else
                    {
                        _logger.LogFullException(e, $"{taskInfo} Profile Operations Problem: {e.Message}");
                    }

                    throw e;
                }

                //adjust loglevel if necessary
                //                                    _logger.LogLevel = profile.logger.logLevel;
                Logger.logger.ConsoleTraceLevel = profile.logger.ConsoleTraceLevel;
                Logger.logger.FileTraceLevel = profile.logger.FileTraceLevel;
                Logger.logger.SplunkTraceLevel = profile.logger.SplunkTraceLevel;
                Logger.logger.TracePattern = profile.logger.TracePattern;

                //compile the scripts as needed
                foreach (var myscript in profile.rules.scripts)
                {
                    if (myscript.script == null)
                    {
                        _logger.Log(LogLevel.Debug, $"compiling {myscript.name}");
                        _scriptService.Compile(myscript);
                    }
                }


                //let's queue up some work
                _logger.Log(LogLevel.Information, "Life Image Transfer Exchange kickoff start");

                var newTaskID = _taskManager.NewTaskID();
                await kickOff(newTaskID);

                _logger.Log(LogLevel.Information, "Life Image Transfer Exchange kickoff end");

                if (profile.backlogDetection)
                {
                    foreach (var conn in profile.connections)
                    {
                        if (conn.toRules.Count > 0)
                        {
                            profile.backlog = true;
                        }
                    }

                    if (profile.backlog)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Detected Backlog, skipping kickoffInterval");
                        await Task.Delay(profile.backlogInterval, _taskManager.cts.Token);
                        profile.backlog = false;
                    }
                    else
                    {
                        await Task.Delay(profile.KickOffInterval, _taskManager.cts.Token);
                    }
                }
                else
                {
                    await Task.Delay(profile.KickOffInterval, _taskManager.cts.Token);
                }

                RecoveryCount = 0;
            }
            catch (Exception e)
            {
                if (LITETask._shutdown)
                {
                    throw new Exception("Application is terminated");
                };

                if (e.Message.Contains("Replacement Strategy Needs Full LITE.init()"))
                {
                }
                else
                {
                    _logger.Log(LogLevel.Critical, $"{taskInfo} Life Image Transfer Exchange Loop Exception: breaking out of run loop due to unrecoverable exception. {e.Message}");
                    if (e.InnerException != null)
                    {
                        _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                    }
                }
            }
        }

        private async Task kickOff(int taskID)
        {
            var profile = _profileStorage.Current;
            var taskInfo = $"task: {taskID}";

            try
            {
                //2018-08-16 shb moved from init to enable task restartability after task cancellation.
#if (DEBUG)
                if (_taskManager.CanStart("ReadConsole"))
                {
                    var newTaskID = _taskManager.NewTaskID();
                    Task task = new Task(new Action(async () => await ReadConsole()));
                    await _taskManager.Start(newTaskID, task, $"ReadConsole", isLongRunning: true);
                }
#endif
                /*
               2018-07-05 shb responsive status reports as soon as task completes 
                */
                if (_taskManager.CanStart("TaskCompletion"))
                {
                    var newTaskID = _taskManager.NewTaskID();
                    Task task = new Task(new Action(async () => await _taskManager.TaskCompletion(profile)));
                    await _taskManager.Start(newTaskID, task, $"TaskCompletion", isLongRunning: true);
                }

                var cloudConnection = _connectionFinder.GetPrimaryLifeImageConnection(profile);

                var connectionManager = _connectionManagerFactory.GetManager(cloudConnection) as ILifeImageCloudConnectionManager;
                if (cloudConnection.loginNeeded)
                {
                    await connectionManager.login(taskID);
                    //await profile.GetPrimaryLifeImageConnection().login(taskID);
                }

                if (!cloudConnection.loginNeeded)
                {
                    kickOffCount++;
                    profile.lastKickOff = DateTime.Now;

                    _logger.Log(LogLevel.Debug, $"{taskInfo} kickOffCount: {kickOffCount}");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} -----------------KickOff----------------");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Processing UpgradeDowngrade");
                    UpgradeDowngrade();

                    _logger.Log(LogLevel.Debug, $"{taskInfo} Processing Run");
                    Run();

                    /*
                     2018-07-06 shb purge async
                      */
                    if ((kickOffCount + 9) % 10 == 0)
                    {
                        if (_taskManager.CanStart("Purge"))
                        {
                            var newTaskID = _taskManager.NewTaskID();
                            Task task = new Task(new Action(async () => await _litePurgeService.Purge(newTaskID)));
                            await _taskManager.Start(newTaskID, task, $"Purge", isLongRunning: false);
                        }
                    }

                    // Process the queues in the connections
                    foreach (var conn in profile.connections)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} connection: {conn.name} enabled: {conn.enabled}");

                        if (conn.enabled == false)
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} connection: {conn.name} enabled: {conn.enabled} skipping");
                            continue;
                        }

                        /*
                        2018-05-10 shb prevent re-entrancy problems with Kickoff.
                         */
                        if (_taskManager.CanStart($"{conn.name}.Kickoff") && conn.started)
                        {
                            var connManager = _connectionManagerFactory.GetManager(conn);
                            var newTaskID = _taskManager.NewTaskID();
                            Task task = new Task(new Action(async () => await connManager.Kickoff(newTaskID)), _taskManager.cts.Token);
                            await _taskManager.Start(newTaskID, task, $"{conn.name}.Kickoff", isLongRunning: false);
                        }
                    }

                    /*
                   2018-06-12 shb status is now long running.
                    */
                    if (_taskManager.CanStart($"UpdateStatus"))
                    {
                        var newTaskID = _taskManager.NewTaskID();
                        Task task = new Task(new Action(async () => await _taskManager.UpdateStatus()));
                        await _taskManager.Start(newTaskID, task, $"UpdateStatus", isLongRunning: true);
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Information, "Primary LifeImage account loginNeeded, skipping kickOff until resolved");

                    await connectionManager.login(taskID);
                    //await profile.GetPrimaryLifeImageConnection().login(taskID);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);

                //throw e;
                throw;
            }
        }

        internal static void shutdown(CloudAuthenticationService cloudAuthenticationService, object p)
        {
        }

        private Task ReadConsole()
        {
            _logger.LogInformation("LiteEngine.ReadConsole");
            return Task.CompletedTask;
        }

        private void UpgradeDowngrade()
        {
            _logger.LogInformation("LiteEngine.UpgradeDowngrade");
        }

        private void Run()
        {
            _logger.LogInformation("LiteEngine.Run");
        }

        private void ShowGreetings()
        {
            var greeting =
                " \n" +
                " \n" +
                " Life Image Transfer Exchange " + $"{version}\n" +
                $" UserName: {System.Environment.UserName}\n" +
                $" MachineName: {System.Environment.MachineName}\n" +
                $" UserDomainName: {System.Environment.UserDomainName}\n" +
                $" Platform: {System.Environment.OSVersion.Platform} ({platform})\n" +
                $" OS: {System.Environment.OSVersion}\n" +
                $" ProcessorCount: {System.Environment.ProcessorCount}\n" +
                $" SystemPageSize: {System.Environment.SystemPageSize}" +
                $" WorkingSet: {System.Environment.WorkingSet}" +
                $" TickCount: {System.Environment.TickCount}" +
                // $" dwLength: {memStatus.dwLength}" +
                // $" MemoryLoad: {memStatus.dwMemoryLoad}" +
                // $" AvailExtendedVirtual: {memStatus.ullAvailExtendedVirtual}" +
                // $" AvailPageFile: {memStatus.ullAvailPageFile}" +
                // $" AvailPhys: {memStatus.ullAvailPhys}" +
                // $" AvailVirtual: {memStatus.ullAvailVirtual}" +
                // $" TotalPageFile: {memStatus.ullTotalPageFile}" +
                // $" TotalPhys: {memStatus.ullTotalPhys}" +
                // $" TotalVirtual: {memStatus.ullTotalVirtual}" +
                " \n" +
                " \n" +
                " Parameters:\n" +
                " \n" +
                " logLevel= Verbose | Debug | Information | Warning | Error | Critical | None | Regex\n" +
                " \n";

            _logger.Log(LogLevel.Information, greeting);
        }

        private Profile LoadConfiguration(string[] args)
        {
            var response = _configurationLoader.LoadConfiguration(args, argsList);

            platform = response.Platform;
            version = response.Version;
            var profile = response.Profile;

            _profileStorage.Set(profile);

            //LITE.profile.runningCodeVersion = version;            

            return profile;
        }

        private void StartImpl(string[] args)
        {
            argsList = new List<string>(args);

            var profile = LoadConfiguration(args);

            initConnections(); //moved here so I can intentionally throw an exception

            //Process();

            _taskManager.Start();

            if (LITETask._shutdown)
            {
                throw new Exception("Application should be temrinated due to it's state");
                //break;
            }

            _logger.Log(LogLevel.Warning, $"LITE will reinitialize in {profile.recoveryInterval * RecoveryCount}ms");

            Task.Delay(profile.recoveryInterval * RecoveryCount++).Wait();

            if (LITETask._shutdown)
            {
                throw new Exception("Application should be temrinated due to it's state");
                //break;
            }

            _logger.Log(LogLevel.Warning, $"Initialization Loop Processing invoked. Agent will try to reinitialize in {profile.recoveryInterval * RecoveryCount}ms");
            Task.Delay(profile.recoveryInterval * RecoveryCount++).Wait();
        }

        // things todo before the class is ready to use
        private void initConnections()
        {
            var profile = _profileStorage.Current;

            Console.CancelKeyPress += Console_CancelKeyPress;

            _connectionsInitializer.InitConnections(profile, argsList, ProfileLocker);
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Stop();
        }
    }
}
