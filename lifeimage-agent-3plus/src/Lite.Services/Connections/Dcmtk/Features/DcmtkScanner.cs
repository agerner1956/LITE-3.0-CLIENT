using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IDcmtkScanner
    {
        Task Scanner(int taskID, DcmtkConnection Connection);
    }

    public sealed class DcmtkScanner : IDcmtkScanner
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILogger _logger;
        private readonly IDiskUtils _util;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IDcmtkDumpService _dcmDumpService;
        private readonly ILITETask _taskManager;

        public DcmtkScanner(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IDiskUtils util,
            IDcmtkDumpService dcmDumpService,
            ILITETask taskManager,
            ILogger<DcmtkScanner> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _dcmDumpService = dcmDumpService;
            _util = util;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task Scanner(int taskID, DcmtkConnection Connection)
        {
            var profile = _profileStorage.Current;

            try
            {
                await Task.Run(async () =>
                {
                    await ScanImpl(profile, Connection, taskID);
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
        }

        private async Task ScanImpl(Profile profile, DcmtkConnection Connection, int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                var dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toScanner";
                Directory.CreateDirectory(dir);
                var fileEntries = _util.DirSearch(dir, "*");
                //var dd = new DuplicatesDetection();
                if (profile.duplicatesDetectionUpload)
                {
                    _logger.Log(LogLevel.Information,
                        $"{taskInfo} toCloud: {(fileEntries == null ? 0 : fileEntries.Count)} files to be send to cloud (before duplicates elimination).");
                }
                else
                {
                    _logger.Log(LogLevel.Information,
                        $"{taskInfo} toCloud: {(fileEntries == null ? 0 : fileEntries.Count)} files to be send to cloud.");
                }

                foreach (string file in fileEntries)
                {
                    if (_taskManager.cts.IsCancellationRequested)
                    {
                        break;
                    }

                    _logger.Log(LogLevel.Debug, $"{taskInfo} Found {file}");
                    RoutedItem routedItem = new RoutedItem(fromConnection: Connection.name, sourceFileName: file, taskID: taskID)
                    {
                        type = RoutedItem.Type.DICOM
                    };

                    routedItem = await _dcmDumpService.DcmDump(taskID, routedItem, Connection);
                    try
                    {
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e);
                    }
                }
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
                _taskManager.Stop($"{Connection.name}.Scanner");
            }
        }
    }
}
