using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lite.Services
{
    public sealed class ProfileConnectionsInitializer : IProfileConnectionsInitializer
    {
        private readonly IProfileManager _profileManager;
        private readonly IConnectionFinder _connectionFinder;
        private readonly IConnectionManagerFactory _connectionManagerFactory;
        private readonly ILogger _logger;

        public ProfileConnectionsInitializer(
            IProfileManager profileManager,
            IConnectionFinder connectionFinder,
            IConnectionManagerFactory connectionManagerFactory,
            ILogger<ProfileConnectionsInitializer> logger)
        {
            _profileManager = profileManager;
            _connectionFinder = connectionFinder;
            _connectionManagerFactory = connectionManagerFactory;
            _logger = logger;
        }

        public void InitConnections(Profile profile, List<string> argsList, object ProfileLocker)
        {
            Throw.IfNull(profile);

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
    }
}
