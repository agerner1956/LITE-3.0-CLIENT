using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILiteDownloadService
    {
        Task Download(int taskID, LITEConnection connection, IHttpManager httpManager, SemaphoreSlim FromEGSSignal);
    }

    public sealed class LiteDownloadService : ILiteDownloadService
    {
        private readonly IGetLiteReresourcesService _getLiteReresourcesService;
        private readonly IDownloadViaHttpService _downloadViaHttpService;
        private readonly IDeleteEGSResourceService _deleteEGSResourceService;        
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public LiteDownloadService(
            IGetLiteReresourcesService getLiteReresourcesService,
            IDownloadViaHttpService downloadViaHttpService,
            IDeleteEGSResourceService deleteEGSResourceService,
            IRoutedItemManager routedItemManager,
            IProfileStorage profileStorage,
            ILITETask taskManager,            
            ILogger<LiteDownloadService> logger)
        {
            _getLiteReresourcesService = getLiteReresourcesService;
            _downloadViaHttpService = downloadViaHttpService;
            _deleteEGSResourceService = deleteEGSResourceService;            
            _routedItemManager = routedItemManager;
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public async Task Download(int taskID, LITEConnection connection, IHttpManager httpManager, SemaphoreSlim FromEGSSignal)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var profile = _profileStorage.Current;

            DateTime lastGetResources = DateTime.MinValue;
            int GetResourcesInterval = 120000;
            int lastResourceCount = 0;
            try
            {
                do
                {
                    var temp = Connection.fromEGS.ToList();
                    if (temp.Count == 0 || !temp.Any(e => e.attempts == 0))
                    {
                        var getResourcesTime = DateTime.Now.AddMilliseconds(GetResourcesInterval * -1);
                        if (lastGetResources.CompareTo(getResourcesTime) < 0 || lastResourceCount > 0)
                        {
                            lastResourceCount = await _getLiteReresourcesService.GetResources(taskID, connection, httpManager);
                            lastGetResources = DateTime.Now;
                        }

                        bool success = await FromEGSSignal.WaitAsync(profile.KickOffInterval, _taskManager.cts.Token).ConfigureAwait(false);
                        // FromEGSSignal.Dispose();
                        // FromEGSSignal = new SemaphoreSlim(0, 1);
                    }
                    else
                    {
                        await Task.Delay(profile.taskDelay).ConfigureAwait(false);
                    }

                    foreach (var routedItem in Connection.fromEGS.ToArray())
                    {
                        await ProcessItem(taskID, routedItem, connection, httpManager);
                    }

                    Task.WaitAll(_taskManager.FindByType($"{Connection.name}.DownloadViaHttp"));

                } while (Connection.responsive);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, $"Wait Operation Canceled. Exiting Download");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
                _logger.Log(LogLevel.Critical, $"{taskInfo} Exiting Download");
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.Download");
            }
        }

        private async Task ProcessItem(int taskID, RoutedItem routedItem, LITEConnection connection, IHttpManager httpManager)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            if (routedItem.lastAttempt == null || routedItem.lastAttempt >= DateTime.Now.AddMinutes(-connection.retryDelayMinutes)) //not attempted lately
            {
                await Task.CompletedTask;
                return;
            }

            routedItem.attempts++;
            if (routedItem.attempts > 1)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} {routedItem.sourceFileName} second attempt.");
            }
            routedItem.lastAttempt = DateTime.Now;

            if (routedItem.attempts > connection.maxAttempts)
            {
                _logger.Log(LogLevel.Warning, $"Resource {routedItem.resource} exceeded max attempts.  Deleting item.");

                if (await _deleteEGSResourceService.DeleteEGSResource(taskID, routedItem, connection, httpManager))
                {
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(connection, connection.fromEGS, nameof(connection.fromEGS), error: false);
                }
                else
                {
                    routedItem.Error = "Exceeded maxAttempts";
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(connection, connection.fromEGS, nameof(connection.fromEGS), error: true);
                }
            }

            switch (Connection.protocol)
            {
                case Protocol.Http:
                    //  while (!LITETask.CanStart($"{name}.DownloadViaHttp"))
                    //  {
                    //      await Task.Delay(LITE.profile.taskDelay).ConfigureAwait(false);
                    //  }
                    var newTaskID = _taskManager.NewTaskID();
                    Task task = new Task(new Action(async () => await _downloadViaHttpService.DownloadViaHttp(newTaskID, routedItem, connection, httpManager)), _taskManager.cts.Token);
                    await _taskManager.Start(newTaskID, task, $"{Connection.name}.DownloadViaHttp", routedItem.resource, isLongRunning: false).ConfigureAwait(false);
                    //await DownloadViaHttp(newTaskID, ri).ConfigureAwait(false);
                    break;
                    // case Protocol.UDT:
                    //     await DownloadViaUDTShell(remoteHostname, remotePort, $"{routedItem.box + "/" + routedItem.resource}", LITE.profile.tempPath + Path.DirectorySeparatorChar + "toScanner", taskID);
                    //     break;
            }
        }
    }
}
