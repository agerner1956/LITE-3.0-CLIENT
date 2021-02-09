using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Lite.Services
{
    public sealed class ProfileLoaderService : IProfileLoaderService
    {
        private readonly IProfileJsonHelper _jsonHelper;
        private readonly ILogger _logger;
        private readonly IUtil _util;

        public ProfileLoaderService(
            IProfileJsonHelper jsonHelper, 
            IUtil util, 
            ILogger<ProfileLoaderService> logger)
        {
            _jsonHelper = jsonHelper;
            _logger = logger;
            _util = util;
        }

        public Profile Load(string fileName)
        {
            _logger.Log(LogLevel.Information, $"Loading: {fileName}");

            string json = File.ReadAllText(fileName);

            if (json == null || json == "" || !json.StartsWith("{"))
            {
                //BOUR-994 try to load the .backup
                json = File.ReadAllText(_util.GetTempFolder(fileName + ".backup"));

                if (json == null || json == "" || !json.StartsWith("{"))
                {
                    throw new Exception("Local Profile is null or blank");
                }
                else
                {
                    _logger.Log(LogLevel.Critical, $"Profile was corrupt, recovered from backup!!");
                }
            }
            return _jsonHelper.DeserializeObject(json);
        }

        public Profile LoadStartupConfiguration(string startupConfigFilePath)
        {
            _logger.Log(LogLevel.Information, $"Loading startup config: {startupConfigFilePath}");

            string json = File.ReadAllText(startupConfigFilePath);

            return _jsonHelper.DeserializeObject(json);
        }
    }
}
