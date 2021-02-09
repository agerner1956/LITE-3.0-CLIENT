using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILiteConnectionPurgeService
    {
        void Purge(int taskID, LITEConnection connection);
    }

    public sealed class LiteConnectionPurgeService : ILiteConnectionPurgeService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;
        private readonly ILogger _logger;

        public LiteConnectionPurgeService(
            IDiskUtils util,
            IProfileStorage profileStorage,
            ILogger<LiteConnectionPurgeService> logger)
        {
            _util = util;
            _profileStorage = profileStorage;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public void Purge(int taskID, LITEConnection connection)
        {
            Connection = connection;
            var taskInfo = $"task: {taskID}";

            var profile = _profileStorage.Current;

            //purge temp
            Directory.CreateDirectory(Connection.resourcePath);
            _logger.Log(LogLevel.Debug, $"{taskInfo} Purging {Connection.resourcePath}");

            var resources = _util.DirSearch(Connection.resourcePath, "*.*");
            foreach (var file in resources)
            {
                try
                {
                    PurgeFile(profile, file, taskInfo);
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e);
                }
            }

            _util.CleanUpDirectory(Connection.resourcePath);
        }

        private void PurgeFile(Profile profile, string file, string taskInfo)
        {
            if (!File.Exists(file))
            {
                return;
            }

            var attr = File.GetAttributes(file);
            if (attr.HasFlag(FileAttributes.Hidden))
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Deleting hidden file {file}.");

                //delete hidden files like .DS_Store
                _util.DeleteAndForget(file, taskInfo);
                return;
            }

            var lastaccesstime = File.GetLastAccessTime(file);
            var creationtime = File.GetCreationTime(file);
            var lastwritetime = File.GetLastWriteTime(file);
            var purgetime = DateTime.Now.AddHours(profile.tempFileRetentionHours * -1);
            if (lastwritetime.CompareTo(purgetime) < 0
            && lastaccesstime.CompareTo(purgetime) < 0
            && creationtime.CompareTo(purgetime) < 0)
            {
                _logger.Log(LogLevel.Debug, $"Purging: {file}");
                _util.DeleteAndForget(file, taskInfo);
            }
        }
    }
}
