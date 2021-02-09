using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Lite.Services.Configuration
{
    public interface IConfigurationLoader
    {
        ConfigurationLoaderPackage LoadConfiguration(string[] args, List<string> argsList);
    }

    public sealed class ConfigurationLoaderPackage
    {
        public ConfigurationLoaderPackage(Profile profile, string platform, string version)
        {
            Profile = profile;
            Platform = platform;
            Version = version;
        }

        public Profile Profile { get; private set; }
        public string Platform { get; private set; }
        public string Version { get; private set; }
    }


    public sealed class ConfigurationLoader : IConfigurationLoader
    {
        private readonly IUtil _util;
        private readonly IDcmtkUtil _dcmtkUtil;
        private readonly ILiteConfigService _liteConfigService;
        private readonly IProfileLoaderService _profileLoaderService;
        private readonly IFileProfileWriter _profileFileWriter;
        private readonly ILogger _logger;

        public ConfigurationLoader(
            IUtil util,
            IDcmtkUtil dcmtkUtil,
            ILiteConfigService liteConfigService,
            IProfileLoaderService profileLoaderService,
            IFileProfileWriter fileProfileWriter,
            ILogger<ConfigurationLoader> logger)
        {
            _util = util;
            _dcmtkUtil = dcmtkUtil;
            _liteConfigService = liteConfigService;
            _profileLoaderService = profileLoaderService;
            _profileFileWriter = fileProfileWriter;
            _logger = logger;
        }

        public ConfigurationLoaderPackage LoadConfiguration(string[] args, List<string> argsList)
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = fvi.FileVersion;

            SaveInRegistry(version);

            var newCurrentDir = assembly.Location.Substring(0, assembly.Location.LastIndexOf(Path.DirectorySeparatorChar));
            Directory.SetCurrentDirectory(newCurrentDir);

            _logger.LogInformation($"LiTE current directory = {newCurrentDir}");

            var currentDir = Directory.GetCurrentDirectory();

            _logger.Log(LogLevel.Information, $"Life Image Transfer Exchange v{version} Started");

            var stopWatch = new Stopwatch();

            var taskInfo = $"";

            stopWatch.Start();

            ExtractWindowsDCMTK();
            var platform = DetectPlatform();

            string startupConfigFilePath = _liteConfigService.GetDefaults().StartupConfigFilePath;
            _logger.Log(LogLevel.Warning, $"startupConfigFilePath: {startupConfigFilePath} Profiles: {_util.GetTempFolder(Constants.Dirs.Profiles)}");

            //2019-05-22 shb copy profile files to working folder.  Needed for read-only installs such as OSX .app bundle and docker/kubernetes
            if (!Directory.Exists(_util.GetTempFolder(Constants.Dirs.Profiles)))
            {
                _logger.Log(LogLevel.Warning, $"{_util.GetTempFolder(Constants.Dirs.Profiles)} does not exist.");
                _logger.Log(LogLevel.Warning, $"Current Directory: {Directory.GetCurrentDirectory()}");

                //TODO check legacy folder locations for existing profile and copy to new location
                if (Directory.Exists(Constants.ProgramDataDir + Path.DirectorySeparatorChar + Constants.Dirs.Profiles))
                {
                    _logger.Log(LogLevel.Warning, $"Copying legacy profiles {Constants.ProgramDataDir + Path.DirectorySeparatorChar + Constants.Dirs.Profiles}");

                    Directory.CreateDirectory(_util.GetTempFolder(Constants.Dirs.Profiles));
                    foreach (string filename in Directory.EnumerateFiles(Constants.ProgramDataDir + Path.DirectorySeparatorChar + Constants.Dirs.Profiles))
                    {
                        var destfile = _util.GetTempFolder(Constants.Dirs.Profiles) + Path.DirectorySeparatorChar + filename.Substring(filename.LastIndexOf(Path.DirectorySeparatorChar));
                        _logger.Log(LogLevel.Information, $"Copying {filename} to {destfile}");

                        using FileStream SourceStream = File.Open(filename, FileMode.Open, FileAccess.Read);
                        using FileStream DestinationStream = File.Create(destfile);
                        SourceStream.CopyTo(DestinationStream);
                    }
                }
                else if (Directory.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.Dirs.Profiles))
                {
                    _logger.Log(LogLevel.Warning, $"Copying default profiles {Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.Dirs.Profiles}");

                    Directory.CreateDirectory(_util.GetTempFolder(Constants.Dirs.Profiles));
                    foreach (string filename in Directory.EnumerateFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.Dirs.Profiles))
                    {
                        var destfile = _util.GetTempFolder(Constants.Dirs.Profiles) + Path.DirectorySeparatorChar + filename.Substring(filename.LastIndexOf(Path.DirectorySeparatorChar));
                        _logger.Log(LogLevel.Information, $"Copying {filename} to {destfile}");

                        using FileStream SourceStream = File.Open(filename, FileMode.Open, FileAccess.Read);
                        using FileStream DestinationStream = File.Create(destfile);
                        SourceStream.CopyTo(DestinationStream);
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"{Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.Dirs.Profiles} does not exist.");
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
            //LITE.profile.runningCodeVersion = version;

            _logger.Log(LogLevel.Information, $"{taskInfo} Running time: {stopWatch.Elapsed}");

            return new ConfigurationLoaderPackage(profile, platform, version);
        }

        private void SaveInRegistry(string version)
        {
            IRegistryHelper registryHelper = new RegistryHelper(_logger);
            registryHelper.RegisterProduct(version);
        }

        private string DetectPlatform()
        {
            var platform = "win";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == true)
            {
                platform = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) == true)
            {
                platform = "osx";
            }

            return platform;
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
    }
}
