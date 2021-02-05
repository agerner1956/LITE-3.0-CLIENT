using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;

namespace Lite.Services
{
    public sealed class FileProfileWriter : IFileProfileWriter
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IUtil _util;
        private readonly ILogger _logger;

        public FileProfileWriter(
            IProfileStorage profileStorage,
            IUtil util,
            ILogger<FileProfileWriter> logger)
        {
            _profileStorage = profileStorage;
            _logger = logger;
            _util = util;
        }

        public void Save(Profile profile, string fileName)
        {
            try
            {
                _logger.Log(LogLevel.Information, $"Saving {fileName}");

                JsonSerializerOptions settings = new JsonSerializerOptions
                {
                    //               TypeNameHandling = TypeNameHandling.All,
                    //Formatting = Formatting.Indented,                   
                };

                string json = JsonSerializer.Serialize(profile, settings);

                if (json == null || json == "" || !json.StartsWith("{"))
                {
                    throw new Exception("json is empty or null");
                }

                if (!_util.DiskUtils.IsDiskAvailable(fileName,_profileStorage.Current, 1000000))
                {
                    throw new Exception($"Insufficient disk to write {fileName}");
                }

                //2018-03-07 shb using WriteAllText because it is atomic and doesn't open the file first and fail during json serialization.
                //2019-05-09 shb using write-through with .backup to help solve improper vmware shutdown
                //File.WriteAllText(Util.GetTempFolder(fileName), json);
                _util.WriteAllTextWithBackup(fileName, json);

                //LifeImageLite.Logger.logger.Log(TraceEventType.Information, $"Saved {fileName}");
                _logger.Log(LogLevel.Information, $"Saved {fileName}");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
                throw;
            }
        }
    }
}
