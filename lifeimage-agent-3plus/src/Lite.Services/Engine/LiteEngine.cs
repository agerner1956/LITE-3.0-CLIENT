using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections;
using Lite.Services.Connections.Cloud;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LiteEngine : ILiteEngine
    {
        private readonly IUtil _util;
        private readonly IScriptService _scriptService;
        private readonly ILiteConfigService _liteConfigService;
        private readonly IProfileLoaderService _profileLoaderService;
        private readonly IProfileManager _profileManager;
        private readonly IProfileWriter _profileWriter;
        private readonly IFileProfileWriter _profileFileWriter;
        private readonly IConnectionManagerFactory _connectionManagerFactory;
        private readonly ICloudProfileLoaderService _cloudProfileLoaderService;
        private readonly IConnectionFinder _connectionFinder;        
        private readonly IProfileStorage _profileStorage;
        private readonly IDcmtkUtil _dcmtkUtil;
        private readonly ILitePurgeService _litePurgeService;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public LiteEngine(
            IUtil util,
            IProfileManager profileManager,
            IScriptService scriptService,
            ILiteConfigService liteConfigService,
            IProfileLoaderService profileLoaderService,
            IProfileWriter profileWriter,
            IFileProfileWriter fileProfileWriter,
            IConnectionManagerFactory connectionManagerFactory,
            ICloudProfileLoaderService cloudProfileLoaderService,
            ILITETask liteTaskManager,
            IProfileStorage profileStorage,
            IConnectionFinder connectionFinder,
            IDcmtkUtil dcmtkUtil,
            ILitePurgeService litePurgeService,
            ILogger<LiteEngine> logger)
        {
            Throw.IfNull(util);
            _util = util;
            _profileManager = profileManager;
            _liteConfigService = liteConfigService;
            _profileLoaderService = profileLoaderService;
            _profileWriter = profileWriter;
            _profileFileWriter = fileProfileWriter;
            _connectionManagerFactory = connectionManagerFactory;
            _cloudProfileLoaderService = cloudProfileLoaderService;
            _scriptService = scriptService;
            _taskManager = liteTaskManager;
            _profileStorage = profileStorage;
            _dcmtkUtil = dcmtkUtil;
            _connectionFinder = connectionFinder;
            _litePurgeService = litePurgeService;
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
            if (profile != null)
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
                            profile.dcmtkLibPath = "tools" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dcmtk-3.6.3-win64-dynamic";
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
                        _logger.Log(LogLevel.Critical, $"{taskInfo} Profile Operations Problem: {e.Message} {e.StackTrace}");
                        if (e.InnerException != null)
                        {
                            _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                        }
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

        private void SaveInRegistry()
        {
            IRegistryHelper registryHelper = new RegistryHelper(_logger);
            registryHelper.RegisterProduct(version);
        }

        private void ExtractWindowsDCMTK()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Directory.Exists("tools/dcmtk/dcmtk-3.6.3-win64-dynamic"))
            {
                Console.WriteLine("Platform is windows and DCMTK is not extracted...");
                try
                {
                    _dcmtkUtil.ExtractWindowsDCMTK();
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Error, "Could not extract dcmtk " + e.Message);
                }
            }
        }

        private void DetectPlatform()
        {
            platform = "win";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == true)
            {
                platform = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) == true)
            {
                platform = "osx";
            }
        }

        private Profile LoadConfiguration(string[] args)
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            version = fvi.FileVersion;

            SaveInRegistry();

            var newCurrentDir = assembly.Location.Substring(0, assembly.Location.LastIndexOf(Path.DirectorySeparatorChar));
            Directory.SetCurrentDirectory(newCurrentDir);

            _logger.LogInformation($"LiTE current directory = {newCurrentDir}");

            var currentDir = Directory.GetCurrentDirectory();

            _logger.Log(LogLevel.Information, $"Life Image Transfer Exchange v{version} Started");

            var stopWatch = new Stopwatch();

            var taskInfo = $"";

            stopWatch.Start();

            ExtractWindowsDCMTK();
            DetectPlatform();

            string startupConfigFilePath = _liteConfigService.GetDefaults().StartupConfigFilePath;
            _logger.Log(LogLevel.Warning, $"startupConfigFilePath: {startupConfigFilePath} Profiles: {_util.GetTempFolder(Constants.ProfilesDir)}");

            //2019-05-22 shb copy profile files to working folder.  Needed for read-only installs such as OSX .app bundle and docker/kubernetes
            if (!Directory.Exists(_util.GetTempFolder(Constants.ProfilesDir)))
            {
                _logger.Log(LogLevel.Warning, $"{_util.GetTempFolder(Constants.ProfilesDir)} does not exist.");
                _logger.Log(LogLevel.Warning, $"Current Directory: {Directory.GetCurrentDirectory()}");

                //TODO check legacy folder locations for existing profile and copy to new location
                if (Directory.Exists(Constants.ProgramDataDir + Path.DirectorySeparatorChar + Constants.ProfilesDir))
                {
                    _logger.Log(LogLevel.Warning, $"Copying legacy profiles {Constants.ProgramDataDir + Path.DirectorySeparatorChar + Constants.ProfilesDir}");

                    Directory.CreateDirectory(_util.GetTempFolder(Constants.ProfilesDir));
                    foreach (string filename in Directory.EnumerateFiles(Constants.ProgramDataDir + Path.DirectorySeparatorChar + Constants.ProfilesDir))
                    {
                        var destfile = _util.GetTempFolder(Constants.ProfilesDir) + Path.DirectorySeparatorChar + filename.Substring(filename.LastIndexOf(Path.DirectorySeparatorChar));
                        _logger.Log(LogLevel.Information, $"Copying {filename} to {destfile}");

                        using FileStream SourceStream = File.Open(filename, FileMode.Open, FileAccess.Read);
                        using FileStream DestinationStream = File.Create(destfile);
                        SourceStream.CopyTo(DestinationStream);
                    }
                }
                else if (Directory.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.ProfilesDir))
                {
                    _logger.Log(LogLevel.Warning, $"Copying default profiles {Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.ProfilesDir}");

                    Directory.CreateDirectory(_util.GetTempFolder(Constants.ProfilesDir));
                    foreach (string filename in Directory.EnumerateFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.ProfilesDir))
                    {
                        var destfile = _util.GetTempFolder(Constants.ProfilesDir) + Path.DirectorySeparatorChar + filename.Substring(filename.LastIndexOf(Path.DirectorySeparatorChar));
                        _logger.Log(LogLevel.Information, $"Copying {filename} to {destfile}");

                        using FileStream SourceStream = File.Open(filename, FileMode.Open, FileAccess.Read);
                        using FileStream DestinationStream = File.Create(destfile);
                        SourceStream.CopyTo(DestinationStream);
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"{Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.ProfilesDir} does not exist.");
                }
            }

            var startupProfile = _profileLoaderService.LoadStartupConfiguration(startupConfigFilePath);
            if (startupProfile.startupParams.localProfilePath != _util.GetTempFolder(startupProfile.startupParams.localProfilePath))
            {
                _logger.Log(LogLevel.Warning, $"Changing startupProfile.startupParams.localProfilePath: {startupProfile.startupParams.localProfilePath} to {_util.GetTempFolder(startupProfile.startupParams.localProfilePath)}");

                startupProfile.startupParams.localProfilePath = _util.GetTempFolder(startupProfile.startupParams.localProfilePath);
                startupProfile.startupParams.saveProfilePath = _util.GetTempFolder(startupProfile.startupParams.saveProfilePath);
                _profileFileWriter.Save(startupProfile, startupConfigFilePath);
            }

            // ulong installedMemory;
            // MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            // if (GlobalMemoryStatusEx(memStatus))
            // {
            //     installedMemory = memStatus.ullTotalPhys;
            // }


            //ShowGreetings();

            // load the logger
            //var profile = new Profile(Logger.logger);
            var profile = new Profile();
            //profile.logger

            // todo: load logger

            // check for setup
            // if (args.Length != 0 && args[0].ToLower() != "setup")
            // {
            //     profile = loadArgs(args, profile);   // re-assign, can be replaced

            //     LITE.SaveToStartupConfigration(profile);
            // }
            // else
            // {
            //startupProfile = Profile.LoadStartupConfiguration();
            startupProfile = _profileLoaderService.LoadStartupConfiguration(startupConfigFilePath);
            profile = startupProfile;
            //                    }

            //                    _logger.LogLevel = profile.logger.logLevel;
            Logger.logger.ConsoleTraceLevel = profile.logger.ConsoleTraceLevel;
            Logger.logger.FileTraceLevel = profile.logger.FileTraceLevel;
            Logger.logger.SplunkTraceLevel = profile.logger.SplunkTraceLevel;
            Logger.logger.TracePattern = profile.logger.TracePattern;

            _logger.Log(LogLevel.Debug, $"Startup Configuration: {profile}");

            //setup
            if (args != null && args.Length > 0 && args[0] != null && args[0] == "setup")
            {
                bool exitCode = false;

                if (args.Length == 1)
                {
                    Setup.EnterSetup(profile);
                }
                else
                {
                    var arg = argsList.Find(x => x.Contains("register="));
                    if (arg != null)
                    {
                        var regParamList = argsList.Find(x => x.Contains("register=")).Substring(9);

                        var regParams = regParamList.Split(',');
                        if (regParams.Length == 4)
                        {
                            exitCode = Setup.Register(regParams, profile);
                            _util.ConfigureService(true);
                        }
                        else
                        {
                            _logger.Log(LogLevel.Information, "Lite Registration: Lite register=\"username,password,orgcode,servicename\"");
                        }
                    }
                    else
                    {
                        var argUninstall = argsList.Find(x => x.Contains("uninstall"));
                        if (argUninstall != null)
                        {
                            _util.ConfigureService(false);
                        }
                    }
                }

                Environment.Exit((exitCode == false) ? 0 : 1);
            }

            // recovery loop
            _logger.Log(LogLevel.Information, "Life Image Transfer Exchange - Starting Processing");
            profile.lastStartup = DateTime.Now;

            //instantiate the class instance

            //var lite = new LITE(profile);            

            profile.runningCodeVersion = version;
            _profileStorage.Set(profile);
            //LITE.profile.runningCodeVersion = version;

            _logger.Log(LogLevel.Information, $"{taskInfo} Running time: {stopWatch.Elapsed}");

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

            try
            {
                //if (profile.startupParams.generateSchema == true)
                //{
                //    Directory.CreateDirectory("JSONSchema");

                //    Logger.logger.Log(TraceEventType.Information, "Registering Newtonsoft.Json.Schema.License.");
                //    Newtonsoft.Json.Schema.License.RegisterLicense(
                //        "3606-tXIhm+ANdVeBkIPZuD16/AYNS41W5oqeEbwG065ms41NYxGKi2qqOtqI9rjSh4TTaOAnj9dYS0hk6cFHAoKcuFJZXg9dhTaBzx/Hx7Oq43qE7bJyWPyHaTaGRMZlZEq2xiPMzOl9x4gBNkgdLz3sJaG9thZJRLSwCdcp0qXXxZ17IklkIjozNjA2LCJFeHBpcnlEYXRlIjoiMjAxOS0wMy0xNVQyMDoyNDozMS43MzkzNzU0WiIsIlR5cGUiOiJKc29uU2NoZW1hQnVzaW5lc3MifQ==");
                //    Logger.logger.Log(TraceEventType.Information,
                //        "Newtonsoft.Json.Schema.License registration complete.");

                //    var types = Assembly
                //        .GetExecutingAssembly()
                //        .GetTypes();
                //    //.Where(t => t.Namespace.StartsWith("LifeImageLite"));

                //    foreach (var file in Directory.EnumerateFiles("JSONSchema"))
                //    {
                //        File.Delete(file);
                //    }

                //    foreach (var type in types)
                //    {
                //        if (type.IsPublic && type.Namespace == "LifeImageLite" && type.Name == "Profile")
                //        {
                //            string schemaString = null;
                //            try
                //            {
                //                JSchemaGenerator generator = new JSchemaGenerator();
                //                JSchema schema = generator.Generate(type);

                //                schemaString = schema.ToString();
                //                if (Logger.logger.FileTraceLevel == "Verbose")
                //                    Logger.logger.Log(TraceEventType.Verbose, $"JSON Schema for {type}");
                //                if (Logger.logger.FileTraceLevel == "Verbose")
                //                    Logger.logger.Log(TraceEventType.Verbose, $"{schemaString}");

                //                if (schemaString != null && !type.Name.Contains("<"))
                //                {
                //                    File.WriteAllTextAsync(
                //                        "JSONSchema" + Path.DirectorySeparatorChar + type.Name + ".schema.json",
                //                        schemaString);
                //                }
                //            }

                //            catch (Newtonsoft.Json.Schema.JSchemaException e)
                //            {
                //                Logger.logger.Log(TraceEventType.Warning, $"{e.Message} {e.StackTrace}");
                //            }
                //        }
                //    }
                //}
            }

            catch (NullReferenceException)
            {
                _logger.Log(LogLevel.Information, $"no startupProfile.startupParams.generateSchema.");
            }

            Directory.CreateDirectory(profile.tempPath);

            //Load Profile from file if specified

            var arg = argsList.Find(x => x.Contains("loadProfileFile="));
            if (arg != null)
            {
                var fileName = argsList.Find(x => x.Contains("loadProfileFile=")).Substring(16);
                if (fileName != null)
                {
                    _logger.Log(LogLevel.Information, $"loadProfileFile={fileName}. Loading this profile");
                    lock (ProfileLocker)
                    {
                        profile.startupParams.localProfilePath = fileName;
                        profile.startupParams.saveProfilePath = fileName;
                        //Profile.LoadProfile(profile, profile.startupParams.localProfilePath);
                        //Profile.LoadProfile(profile, profile.startupParams.localProfilePath);
                        _profileManager.LoadProfile(profile, profile.startupParams.localProfilePath);
                    }
                }
            }
            else
            {
                _logger.Log(LogLevel.Information, $"no loadProfileFile arg.  Using startup.config.json");
                _logger.Log(LogLevel.Information, $"profile.startupParams.localProfilePath: {profile.startupParams.localProfilePath}");

                if (profile.startupParams.localProfilePath != null)
                {
                    lock (ProfileLocker)
                    {
                        _profileManager.LoadProfile(profile, profile.startupParams.localProfilePath);
                        //Profile.LoadProfile(profile, profile.startupParams.localProfilePath);
                    }
                }
                else
                {
                    //Find the current profile from the LITE.sh file
                    var currentProfile = File.ReadAllText("LITE.sh");
                    var elements = currentProfile.Split(" ");
                    currentProfile = elements[2].Substring(16);

                    lock (ProfileLocker)
                    {
                        profile.startupParams.localProfilePath = currentProfile;
                        profile.startupParams.saveProfilePath = currentProfile;
                        profile.startupParams.getServerProfile = true;
                        profile.startupParams.putServerProfile = true;
                        profile.startupParams.generateSchema = true;
                        profile.startupParams.validateProfile = true;

                        //Profile.LoadProfile(profile, currentProfile);
                        _profileManager.LoadProfile(profile, currentProfile);
                    }
                }
            }

            _logger.Log(LogLevel.Debug, $"Replacing Logger.logger with Profile.logger");

            //            Logger.logger.logLevel = profile.logger.logLevel;
            Logger.logger.ConsoleTraceLevel = profile.logger.ConsoleTraceLevel;
            Logger.logger.FileTraceLevel = profile.logger.FileTraceLevel;
            Logger.logger.SplunkTraceLevel = profile.logger.SplunkTraceLevel;
            Logger.logger.TracePattern = profile.logger.TracePattern;

            //initialize the connections

            var primary = _connectionFinder.GetPrimaryLifeImageConnection(profile);
            //LifeImageCloudConnection primary = profile.GetPrimaryLifeImageConnection(); /* AMG Why do we need to find primary connection if we are not using it? What is there is more than 1. The code is expecting only one primary connection */
            Rules rules = profile.rules;

            foreach (var conn in profile.connections)
            {
                if (conn.enabled == true)
                {
                    var connManager = _connectionManagerFactory.GetManager(conn);
                    connManager.Init();
                    //conn.init();
                }
            }

            //Create the specified folders
            Directory.CreateDirectory(profile.tempPath);
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Stop();
        }
    }
}
