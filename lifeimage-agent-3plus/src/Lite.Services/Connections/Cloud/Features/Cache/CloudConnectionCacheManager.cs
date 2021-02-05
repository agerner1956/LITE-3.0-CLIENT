using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ICloudConnectionCacheManager
    {
        Task ExpireCache(LifeImageCloudConnection Connection, IConnectionRoutedCacheManager manager, int taskID);
    }

    public sealed class CloudConnectionCacheManager : ICloudConnectionCacheManager
    {
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public CloudConnectionCacheManager(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILogger<CloudConnectionCacheManager> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task ExpireCache(LifeImageCloudConnection Connection, IConnectionRoutedCacheManager manager, int taskID)
        {
            Throw.IfNull(manager);
            
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                while (!_taskManager.cts.IsCancellationRequested)
                {
                    await Task.Delay(_profileStorage.Current.KickOffInterval, _taskManager.cts.Token);

                    //age the response cache before asking for more
                    foreach (var cacheItem in LifeImageCloudConnection.cache.ToArray())
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Cache entry id: {cacheItem.Key}");

                        foreach (var item in cacheItem.Value.ToArray())
                        {
                            ProcessRoutedItem(item, Connection, manager, taskInfo);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (System.InvalidOperationException) //for the Collection was Modified, we can wait
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Waiting for requests to complete before getting new requests");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                try
                {
                    _taskManager.Stop($"{Connection.name}.ExpireCache");
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }
            }
        }

        private void ProcessRoutedItem(RoutedItem item, LifeImageCloudConnection Connection, IConnectionRoutedCacheManager manager, string taskInfo)
        {
            _logger.Log(LogLevel.Debug, $"{taskInfo} fromConnection: {item.fromConnection} id: {item.id} type: {item.type} started: {item.startTime} complete: {item.resultsTime} status: {item.status}");

            if (item.type == RoutedItem.Type.DICOM && item.sourceFileName == null)
            {
                _logger.Log(LogLevel.Critical, $"{taskInfo} DICOM type requires sourceFileName!! fromConnection: {item.fromConnection} id: {item.id} type: {item.type} started: {item.startTime} complete: {item.resultsTime} status: {item.status}");
            }

            DateTime purgetime;
            if (item.type == RoutedItem.Type.RPC)
            {
                purgetime = DateTime.Now.AddMinutes(Connection.MaxRequestAgeMinutes * -1);
            }
            else
            {
                purgetime = DateTime.Now.AddMinutes(Connection.StudyCloseInterval * -1);
            }

            if (item.startTime.CompareTo(purgetime) < 0)
            {
                if (item.startTime == DateTime.MinValue)
                {
                    _logger.Log(LogLevel.Error, $"{taskInfo} id: {item.id} has unassigned start time");
                }

                if (item.type == RoutedItem.Type.RPC)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} id: {item.id} did not complete, purging cache based on Type: {item.type} MaxRequestAgeMinutes:{Connection.MaxRequestAgeMinutes} with calculated purgetime:{purgetime} and closing out");
                    item.status = RoutedItem.Status.FAILED;
                    Dictionary<string, string> returnTagData = new Dictionary<string, string>
                                    {
                                        { "StatusCode", "-1" },
                                        {
                                            "StatusDescription",
                                            $"Error: request: {item.id} Connection: {item.fromConnection} startTime: {item.startTime} did not complete based on maxRequestAgeMinutes: {Connection.MaxRequestAgeMinutes}. Closing out request."
                                        }
                                    };

                    string key = "response";

                    Dictionary<string, Dictionary<string, string>> status =
                        new Dictionary<string, Dictionary<string, string>>
                        {
                                            { key, returnTagData }
                        };

                    string jsonResults = JsonSerializer.Serialize(status);
                    item.response.Add(jsonResults);
                    manager.Route(item); //can't await inside a lock

                    return; //send just one of the items for each request id, not each cached item for the id.
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} id: {item.id} purging cache based on Type: {item.type} StudyCloseInterval:{Connection.StudyCloseInterval} with calculated purgetime:{purgetime} and closing out");
                    _routedItemManager.Init(item);
                    var completionItem = (RoutedItem)_routedItemManager.Clone();
                    completionItem.status = RoutedItem.Status.COMPLETED;
                    completionItem.type = RoutedItem.Type.COMPLETION;
                    completionItem.sourceFileName =
                        null; //a completion can be for a file/study that was previously routed so the file refererence is old and now meaningless.
                    manager.Route(completionItem);

                    return; //send just one of the items for each request id, not each cached item for the id.
                }
            }

            var responseCacheExpiry = DateTime.Now.AddMinutes(Connection.ResponseCacheExpiryMinutes * -1);

            if (item.startTime.CompareTo(responseCacheExpiry) < 0)
            {
                _logger.Log(LogLevel.Information,
                    $"{taskInfo} id: {item.id} did not complete, purging cache based on Type: {item.type} ResponseCacheExpiryMinutes:{Connection.ResponseCacheExpiryMinutes} with calculated responseCacheExpiry:{responseCacheExpiry}");

                manager.RemoveCachedItem(item);
            }
        }
    }
}
