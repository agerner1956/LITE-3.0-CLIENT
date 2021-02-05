using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILiteToEgsService
    {
        /// <summary>
        /// SendToCloud takes the toCloud array and batches it u first by sharing destinations and then by minEGSBatchSize.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="connection"></param>
        /// <param name="sendToAllHubs"></param>
        /// <returns></returns>
        Task SendToEGS(int taskID, LITEConnection connection, ISendToAllHubsService sendToAllHubs);
    }

    public sealed class LiteToEgsService : ILiteToEgsService
    {
        private readonly ILiteStoreService _liteStoreService;
        private readonly ILitePresentAsResourceService _presentAsResourceService;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public LiteToEgsService(
            ILiteStoreService liteStoreService,
            ILitePresentAsResourceService presentAsResourceService,
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILogger<LiteToEgsService> logger)
        {
            _liteStoreService = liteStoreService;
            _presentAsResourceService = presentAsResourceService;
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public async Task SendToEGS(int taskID, LITEConnection connection, ISendToAllHubsService sendToAllHubs)
        {
            Connection = connection;
            //2018-02-02 shb RoutedItem does not necessarily have an open stream at this point any more

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Information, $"{taskInfo} toEGS: {(Connection.toEGS == null ? 0 : Connection.toEGS.Count)} items to send.");

            Dictionary<string, List<RoutedItem>> shareSet = new Dictionary<string, List<RoutedItem>>();  //I need a set for each sharing dest set
            List<List<RoutedItem>> sizeSet = new List<List<RoutedItem>>();  //I need a set for each minEGSBatchSize

            try
            {
                Task.WaitAll(_taskManager.FindByType($"{Connection.name}.PresentAsResource"));
                Task.WaitAll(_taskManager.FindByType($"{Connection.name}.Store"));

                int retryDelayed = 0;

                if (Connection.toEGS.Count > 0)
                {
                    var toEGSTemp = Connection.toEGS.ToArray();
                    //remove the toCloud item if it has exceeded maxAttempts
                    foreach (var routedItem in toEGSTemp)
                    {
                        if (_taskManager.cts.IsCancellationRequested) return;

                        if (routedItem.lastAttempt != null && routedItem.lastAttempt < DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)) //not attempted lately
                        {
                            routedItem.attempts++;
                            if (routedItem.attempts > 1)
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} {routedItem.sourceFileName} second attempt.");
                            }
                            routedItem.lastAttempt = DateTime.Now;

                            if (routedItem.attempts > Connection.maxAttempts)
                            {
                                _logger.Log(LogLevel.Error, $"{taskInfo} {routedItem.sourceFileName} has exceeded maxAttempts of {Connection.maxAttempts}.  Will move to errors and not try again.");

                                routedItem.Error = "Exceeded maxAttempts";

                                _routedItemManager.Init(routedItem);
                                _routedItemManager.Dequeue(Connection, Connection.toEGS, nameof(Connection.toEGS), error: true);
                            }
                            else
                            {
                                _logger.Log(LogLevel.Information, $"{taskInfo} {routedItem.sourceFileName} attempts: {routedItem.attempts}");
                            }

                            //inspect the sharing headers and batch by set
                            string shareString = "";
                            if (Connection.shareDestinations != null)
                            {
                                foreach (var connectionSet in routedItem.toConnections.FindAll(e => e.connectionName.Equals(Connection.name)))
                                {
                                    if (connectionSet.shareDestinations != null)
                                    {
                                        foreach (var shareDestination in connectionSet.shareDestinations)
                                        {
                                            shareString += shareDestination.boxUuid;
                                        }
                                    }
                                }
                            }

                            if (shareSet.ContainsKey(shareString))
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Adding {routedItem.sourceFileName} to shareString: {shareString}");
                                shareSet.GetValueOrDefault(shareString).Add(routedItem);
                            }
                            else
                            {
                                var list = new List<RoutedItem>
                                {
                                    routedItem
                                };
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Adding {routedItem.sourceFileName} to shareString: {shareString}");
                                shareSet.Add(shareString, list);
                            }
                        }
                        else
                        {
                            retryDelayed++;
                        }
                    }

                    //Now that each key in the Dictionary is to a single set of sharing destinations, let's break it up further by minEGSBatchSize
                    //What we want is a big enough upload to solve the small file problem, but small enough so the upload makes forward progress.
                    //If this is not the first attempt, then disable batching and send individually.


                    foreach (var share in shareSet.Values)
                    {
                        if (_taskManager.cts.IsCancellationRequested)
                        {
                            return;
                        }

                        var batch = new List<RoutedItem>();

                        long bytes = 0;
                        foreach (var element in share)
                        {
                            if (File.Exists(element.sourceFileName))
                            {
                                try
                                {
                                    element.length = new FileInfo(element.sourceFileName).Length;
                                }
                                catch (FileNotFoundException e)
                                {
                                    _logger.Log(LogLevel.Error, $"{taskInfo} {e.Message} {e.StackTrace}");

                                    continue;
                                }
                                catch (IOException e)
                                {
                                    _logger.Log(LogLevel.Error, $"{taskInfo} {e.Message} {e.StackTrace}");
                                    //condition may be transient like file in use so skip for the moment
                                    continue;
                                }
                                catch (Exception e)
                                {
                                    _logger.LogFullException(e, taskInfo);
                                    //condition may be transient like file so skip for the moment
                                    continue;
                                }
                            }
                            else
                            {
                                element.Error = $"File {element.sourceFileName} does not exist";
                                _routedItemManager.Init(element);
                                _routedItemManager.Dequeue(Connection, Connection.toEGS, nameof(Connection.toEGS), true);
                            }

                            //If this is not the first attempt, then disable batching and send individually.
                            if (element.length < Connection.minEGSBatchSize && bytes < Connection.minEGSBatchSize && element.attempts == 1)
                            {
                                bytes += element.length;
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Adding {element.sourceFileName} to batch...");
                                batch.Add(element);
                            }
                            else
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Batch is full with count: {batch.Count} size: {bytes} attempts: {element.attempts} {(element.attempts > 1 ? "items are sent individually after 1st attempt!" : "")}");
                                sizeSet.Add(batch);
                                batch = new List<RoutedItem>();
                                bytes = element.length;
                                batch.Add(element);
                            }
                        }

                        if (!sizeSet.Contains(batch) && batch.Count > 0)
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Add final batch to set with count: {batch.Count} size: {bytes}");
                            sizeSet.Add(batch);
                        }
                    }

                    int tempcount = 0;
                    foreach (var batch in sizeSet)
                    {
                        tempcount += batch.Count;
                    }

                    _logger.Log(LogLevel.Information, $"{taskInfo} {sizeSet.Count} batches to send, selected: {tempcount}/{toEGSTemp.Length} retry delayed: {retryDelayed}");

                    foreach (var batch in sizeSet)
                    {
                        if (_taskManager.cts.IsCancellationRequested)
                        {
                            return;
                        }

                        //if (loginNeeded) break;

                        if (batch.Count > 0)
                        {
                            switch (Connection.PushPull)
                            {
                                case PushPullEnum.pull:
                                    switch (Connection.protocol)
                                    {
                                        case Protocol.Http:
                                        case Protocol.UDT:
                                            var newTaskID = _taskManager.NewTaskID();
                                            Task task = new Task(new Action(async () => await _presentAsResourceService.PresentAsResource(batch, newTaskID, connection, sendToAllHubs)));
                                            await _taskManager.Start(newTaskID, task, $"{Connection.name}.PresentAsResource", $"{Connection.name}.PresentAsResource batch {batch.Count}", isLongRunning: false);
                                            break;
                                    }

                                    break;
                                case PushPullEnum.push:
                                    switch (Connection.protocol)
                                    {
                                        case Protocol.Http:
                                            var newTaskID2 = _taskManager.NewTaskID();
                                            Task task2 = new Task(new Action(async () => await _liteStoreService.store(batch, newTaskID2, connection)));
                                            await _taskManager.Start(newTaskID2, task2, $"{Connection.name}.Store", $"{Connection.name}.Store batch {batch.Count}", isLongRunning: false);
                                            break;
                                        case Protocol.UDT:
                                            break;
                                    }

                                    break;
                                case PushPullEnum.both:
                                    //since each method dequeues it's own work we would need a separate queue before we can do both, like toEGSPull toEGSPush.
                                    break;
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }
    }
}
