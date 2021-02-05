//using Dicom.Log;
using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Cache;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public interface IConnectionToRulesManagerAdapter
    {
        Task SendToRules(int taskID, ConnectionToRulesManagerAdapterArgs args, bool responsive = true);
    }

    public class ConnectionToRulesManagerAdapterArgs
    {
        public ConnectionToRulesManagerAdapterArgs(
            Connection connection, 
            BlockingCollection<RoutedItem> toRules,
            Dictionary<string, List<RoutedItem>>  cache,
            IConnectionRoutedCacheManager cacheManager)
        {
            Throw.IfNull(connection);
            Throw.IfNull(toRules);
            Throw.IfNull(cache);

            Connection = connection;
            this.toRules = toRules;
            this.cache = cache;
            connectionRoutedCacheManager = cacheManager;
        }

        public Connection Connection { get; set; }
        public BlockingCollection<RoutedItem> toRules { get; set; }
        public Dictionary<string, List<RoutedItem>> cache { get; set; }
        public IConnectionRoutedCacheManager connectionRoutedCacheManager { get; set; }
    }

    public sealed class ConnectionToRulesManagerAdapter : IConnectionToRulesManagerAdapter
    {
        private readonly IRulesManager _rulesManager;
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IConnectionCacheResponseService _connectionCache;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public ConnectionToRulesManagerAdapter(
            IRulesManager rulesManager,
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IConnectionCacheResponseService connectionCache,
            ILITETask taskManager,
            ILogger<ConnectionToRulesManagerAdapter> logger)
        {
            _rulesManager = rulesManager;
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _connectionCache = connectionCache;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task SendToRules(int taskID, ConnectionToRulesManagerAdapterArgs args, bool responsive = true)
        {
            var Connection = args.Connection;
            var toRules = args.toRules;
            var cache = args.cache;
            IConnectionRoutedCacheManager connectionRoutedCacheManager = args.connectionRoutedCacheManager;

            Throw.IfNull(Connection);

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            //            RoutedItem[] temp;

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering SendToRules.");
            try
            {
                do
                {
                    RoutedItem routedItem = null;
                    var count = 0;
                    //                    bool newItems = false;

                    // lock (toRules)
                    // {
                    //     newItems = toRules.Any(e => e.attempts == 0);
                    // }

                    // if (count == 0 || !newItems)
                    // {
                    //     bool success = await ToRulesSignal.WaitAsync(LITE.profile.kickoffInterval, LITETask.cts.Token).ConfigureAwait(false);
                    // }

                    // lock (toRules)
                    // {
                    //     temp = toRules.ToArray();
                    // }
                    //send everything in toRules
                    // foreach (var routedItem in temp)
                    // {
                    //  if (LITETask.cts.Token.IsCancellationRequested)
                    //   {
                    routedItem = toRules.Take(_taskManager.cts.Token); //item removed from list prior to processing

                    count = toRules.Count + 1;
                    _logger.Log(LogLevel.Information, $"{taskInfo} toRules: {count} items to transfer.");

                    // if (routedItem.lastAttempt != null && routedItem.lastAttempt < DateTime.Now.AddMinutes(-retryDelayMinutes)) //not attempted lately
                    // {
                    routedItem.attempts++;
                    routedItem.lastAttempt = DateTime.Now;
                    try
                    {
                        if (routedItem.attempts <= Connection.maxAttempts)
                        {
                            //  shb 2019-03-15 BOUR-85 to support intelligent routing of items based on pre-determined routing info
                            // EX: hl7 has the routing info while subsequent dicom items do not
                            // Order of precedence
                            // 0.5) No caching for request / response, done elsewhere
                            // 1) No routing info - use connection - based rules
                            // 2) Prior routing info for patientID and accession # - use provider routing rules
                            // 3) Address - oriented routing in item - override all other rules                              
                            if (routedItem.id != null && routedItem.type != RoutedItem.Type.RPC)
                            {
                                routedItem = _connectionCache.CacheResponse(Connection, routedItem, cache); //for a dicom/file item to look up prior toConnections of hl7
                            }

                            var currentProfile = _profileStorage.Current;

                            _rulesManager.Init(currentProfile.rules);
                            routedItem = await _rulesManager.SendToRules(routedItem, _routedItemManager, connectionRoutedCacheManager);

                            if (routedItem != null && routedItem.id != null &&
                                routedItem.type != RoutedItem.Type.RPC)
                            {
                                routedItem = _connectionCache.CacheResponse(Connection,routedItem, cache); //for an hl7 to record the toConnections
                            }

                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, toRules, nameof(toRules), error: false);
                        }
                        else
                        {
                            _logger.Log(LogLevel.Warning, $"{taskInfo} {routedItem.RoutedItemMetaFile} exceeded maxAttempts");

                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, toRules, nameof(toRules), error: true);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, $"{taskInfo} returning item to queue: {routedItem.RoutedItemMetaFile}");

                        if (routedItem != null)
                        {
                            toRules.Add(routedItem, _taskManager.cts.Token);
                        }
                    }
                    //                        } 

                    // ToRulesSignal.Dispose();
                    // ToRulesSignal = new SemaphoreSlim(0, 1);
                    //
                    // }
                    //  } 
                } while (responsive);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.SendToRules");
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} Exiting SendToRules.");
        }
    }
}
