using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ISendToCloudService
    {
        /// <summary>
        /// SendToCloud takes the toCloud array and batches it up first by sharing destinations and then by minStowBatchSize
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="Connection"></param>
        /// <param name="cacheManager"></param>
        /// <param name="httpManager"></param>
        /// <returns></returns>
        Task SendToCloud(int taskID, LifeImageCloudConnection Connection, IConnectionRoutedCacheManager cacheManager, IHttpManager httpManager);
    }

    public sealed class SendToCloudService : ISendToCloudService
    {
        private readonly ILogger _logger;
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IDuplicatesDetectionService _duplicatesDetectionService;
        private readonly IDicomUtil _dicomUtil;
        private readonly IStowAsMultiPartCloudService _stowAsMultiPartCloudService;
        private readonly ISendFromCloudToHl7Service _sendFromCloudToHl7Service;
        private readonly IPostResponseCloudService _postResponseCloudService;
        private readonly IPostCompletionCloudService _postCompletionCloudService;
        private readonly ILITETask _taskManager;

        public SendToCloudService(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IDuplicatesDetectionService duplicatesDetectionService,
            IStowAsMultiPartCloudService stowAsMultiPartCloudService,
            ISendFromCloudToHl7Service sendFromCloudToHl7Service,
            IPostResponseCloudService postResponseCloudService,
            IPostCompletionCloudService postCompletionCloudService,
            IDicomUtil dicomUtil,
            ILITETask taskManager,
            ILogger<SendToCloudService> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _duplicatesDetectionService = duplicatesDetectionService;
            _dicomUtil = dicomUtil;
            _stowAsMultiPartCloudService = stowAsMultiPartCloudService;
            _sendFromCloudToHl7Service = sendFromCloudToHl7Service;
            _postResponseCloudService = postResponseCloudService;
            _postCompletionCloudService = postCompletionCloudService;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task SendToCloud(int taskID, LifeImageCloudConnection Connection, IConnectionRoutedCacheManager cacheManager, IHttpManager httpManager)
        {
            //2018-02-02 shb RoutedItem does not necessarily have an open stream at this point any more

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Information,
                $"{taskInfo} toCloud: {(Connection.toCloud == null ? 0 : Connection.toCloud.Count)} suggested items to send.");

            Dictionary<string, List<RoutedItem>> shareSet = new Dictionary<string, List<RoutedItem>>(); //I need a set for each sharing dest set
            List<List<RoutedItem>> sizeSet = new List<List<RoutedItem>>(); //I need a set for each minStowBatchSize

            try
            {
                Task.WaitAll(_taskManager.FindByType($"{Connection.name}.putHL7"));
                Task.WaitAll(_taskManager.FindByType($"{Connection.name}.PostResponse"));
                Task.WaitAll(_taskManager.FindByType($"{Connection.name}.Stow"));

                int retryDelayed = 0;
                List<RoutedItem> toCloudTemp = new List<RoutedItem>();

                if (Connection.toCloud.Count <= 0)
                {
                    await Task.CompletedTask;
                    return;
                }

                lock (Connection.toCloud)
                {
                    foreach (var routedItem in Connection.toCloud)
                    {
                        if (_profileStorage.Current.duplicatesDetectionUpload && routedItem.type == RoutedItem.Type.DICOM && routedItem.sourceFileName != null)
                        {
                            _duplicatesDetectionService.DuplicatesPurge();
                            lock (routedItem)
                            {
                                if (!_duplicatesDetectionService.DuplicatesReference(routedItem.fromConnection, routedItem.sourceFileName))
                                {
                                    continue;
                                }
                            }
                        }

                        if (routedItem.lastAttempt == null ||
                            (routedItem.lastAttempt != null && routedItem.lastAttempt <
                                DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)))
                        {
                            //not attempted lately
                            routedItem.attempts++;
                            routedItem.lastAttempt = DateTime.Now;

                            if (routedItem.attempts > 1)
                            {
                                _logger.Log(LogLevel.Debug,
                                    $"{taskInfo} type: {routedItem.type} id: {routedItem.id} file: {routedItem.sourceFileName} meta: {routedItem.RoutedItemMetaFile} second attempt.");
                            }

                            toCloudTemp.Add(routedItem);
                        }
                        else
                        {
                            retryDelayed++;
                        }
                    }
                }

                foreach (var routedItem in toCloudTemp)
                {
                    //remove the toCloud item if it has exceeded maxAttempts
                    if (routedItem.attempts > Connection.maxAttempts)
                    {
                        // AMG LITE-1090 - put a break on then execution (add continue statement) and add routedItem status to the message.
                        _logger.Log(LogLevel.Error,
                            $"{taskInfo} type: {routedItem.type} id: {routedItem.id} status: {routedItem.status} file: {routedItem.sourceFileName} meta: {routedItem.RoutedItemMetaFile} has exceeded maxAttempts of {Connection.maxAttempts}.  Will move to errors and not try again (removed from send queue).");

                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud), error: true);
                        continue;
                    }
                    else
                    {
                        _logger.Log(LogLevel.Information,
                            $"{taskInfo}  type: {routedItem.type} id: {routedItem.id} file: {routedItem.sourceFileName} meta: {routedItem.RoutedItemMetaFile} attempt: {routedItem.attempts}");
                    }

                    switch (routedItem.type)
                    {
                        case RoutedItem.Type.RPC:
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} PostResponse ID: {routedItem.id}");
                                var newTaskID = _taskManager.NewTaskID();
                                Task task = new Task(new Action(async () => await _postResponseCloudService.PostResponse(Connection, routedItem, cacheManager, httpManager, newTaskID)), _taskManager.cts.Token);
                                await _taskManager.Start(newTaskID, task, $"{Connection.name}.PostResponse", $"{Connection.name}.PostResponse {routedItem.id}", isLongRunning: false);
                            }
                            break;

                        case RoutedItem.Type.HL7:
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} putHL7 file: {routedItem.sourceFileName}");
                                var newTaskID = _taskManager.NewTaskID();
                                Task task = new Task(new Action(async () => await _sendFromCloudToHl7Service.putHL7(routedItem, newTaskID, Connection, httpManager)), _taskManager.cts.Token);
                                await _taskManager.Start(newTaskID, task, $"{Connection.name}.putHL7", $"{Connection.name}.putHL7 {routedItem.sourceFileName}", isLongRunning: false);
                            }
                            break;

                        case RoutedItem.Type.COMPLETION:
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Completion ID: {routedItem.id} type: {routedItem.type} ");
                                var newTaskID = _taskManager.NewTaskID();
                                Task task = new Task(
                                    new Action(async () => await _postCompletionCloudService.PostCompletion(Connection, routedItem, cacheManager, httpManager, newTaskID)),
                                    _taskManager.cts.Token);
                                await _taskManager.Start(newTaskID, task, $"{Connection.name}.PostCompletion",
                                    $"{Connection.name}.PostCompletion {routedItem.id}", isLongRunning: false);
                            }
                            break;

                        case RoutedItem.Type.DICOM:
                        case RoutedItem.Type.FILE:
                            //check if dicom, if not dicomize since cloud only does dicom via stow.
                            if (File.Exists(routedItem.sourceFileName) && !_dicomUtil.IsDICOM(routedItem))
                            {
                                _dicomUtil.Dicomize(routedItem);
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

                                _logger.Log(LogLevel.Debug,
                                    $"{taskInfo} Adding {routedItem.sourceFileName} to shareString: {shareString}");
                                shareSet.Add(shareString, list);
                            }

                            break;
                        default:
                            _logger.Log(LogLevel.Critical,
                                $"{taskInfo} meta: {routedItem.RoutedItemMetaFile} Unsupported type: {routedItem.type}");
                            break;
                    }
                }

                //Now that each key in the Dictionary is to a single set of sharing destinations, let's break it up further by minStowBatchSize
                //What we want is a big enough upload to solve the small file problem, but small enough so the upload makes forward progress.
                //If this is not the first attempt, then disable batching and send individually.
                foreach (var share in shareSet.Values)
                {
                    var batch = new List<RoutedItem>();

                    long bytes = 0;
                    foreach (var element in share)
                    {
                        try
                        {
                            element.length = new FileInfo(element.sourceFileName).Length;
                        }
                        catch (FileNotFoundException e)
                        {
                            _logger.Log(LogLevel.Critical, $"{taskInfo} id: {element.id} meta:{element.RoutedItemMetaFile} source:{element.sourceFileName} type:{element.type} {e.Message} {e.StackTrace}");

                            _routedItemManager.Init(element);
                            _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud), true);

                            continue;
                        }
                        catch (IOException e)
                        {
                            _logger.Log(LogLevel.Critical, $"{taskInfo} id: {element.id} meta:{element.RoutedItemMetaFile} source:{element.sourceFileName} type:{element.type} {e.Message} {e.StackTrace}");
                            //condition may be transient like file in use so skip for the moment
                            continue;
                        }
                        catch (Exception e)
                        {
                            _logger.Log(LogLevel.Critical, $"{taskInfo} id: {element.id} meta:{element.RoutedItemMetaFile} source:{element.sourceFileName} type:{element.type} {e.Message} {e.StackTrace}");
                            if (e.InnerException != null)
                            {
                                _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                            }

                            //condition may be transient like file so skip for the moment
                            continue;
                        }

                        //If this is not the first attempt, then disable batching and send individually.
                        if (element.length < Connection.minStowBatchSize && bytes < Connection.minStowBatchSize && element.attempts == 1)
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

                _logger.Log(LogLevel.Information,
                    $"{taskInfo} {sizeSet.Count} batches to send, selected: {tempcount}/{toCloudTemp.Count} retry delayed: {retryDelayed}");

                foreach (var batch in sizeSet)
                {
                    if (httpManager.loginNeeded)
                    {
                        break;
                    }

                    if (batch.Count > 0)
                    {
                        var newTaskID = _taskManager.NewTaskID();
                        Task task = new Task(new Action(async () => await _stowAsMultiPartCloudService.stowAsMultiPart(batch, newTaskID, Connection, httpManager)), _taskManager.cts.Token);
                        await _taskManager.Start(newTaskID, task, $"{Connection.name}.Stow", $"{Connection.name}.Stow batch {batch.Count}", isLongRunning: false);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }
    }
}
