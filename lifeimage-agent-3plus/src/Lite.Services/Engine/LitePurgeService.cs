using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LitePurgeService : ILitePurgeService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;
        private readonly ILogger _logger;
        private readonly ILITETask _taskManager;

        public LitePurgeService(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILITETask taskManager,
            ILogger<LitePurgeService> logger)
        {
            _profileStorage = profileStorage;
            _util = util;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task Purge(int taskID)
        {
            var profile = _profileStorage.Current;

            var taskInfo = $"task: {taskID}";

            try
            {
                await Task.Run(() =>
                {
                    PurgeImpl(profile, taskInfo);
                });
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                _taskManager.Stop($"Purge");
            }
        }

        private void PurgeImpl(Profile profile, string taskInfo)
        {
            //purge temp
            Directory.CreateDirectory(profile.tempPath);
            _logger.Log(LogLevel.Debug, $"{taskInfo} Purging {profile.tempPath}");

            var files = _util.DirSearch(profile.tempPath, "*.*");
            foreach (var file in files)
            {
                try
                {
                    ProcessFile(profile, file, taskInfo);
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e);
                }
            }

            _util.CleanUpDirectory(profile.tempPath);
        }

        private void ProcessFile(Profile profile, string file, string taskInfo)
        {
            Throw.IfNull(profile);
            Throw.IfNullOrWhiteSpace(file);

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
