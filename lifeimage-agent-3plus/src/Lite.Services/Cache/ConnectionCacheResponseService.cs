using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lite.Services.Cache
{
    public interface IConnectionCacheResponseService
    {
        RoutedItem CacheResponse(Connection Connection, RoutedItem routedItem, Dictionary<string, List<RoutedItem>> cache);
        void RemoveCachedItem(Connection Connection, RoutedItem routedItem, Dictionary<string, List<RoutedItem>> cache);
    }

    public sealed class ConnectionCacheResponseService : IConnectionCacheResponseService
    {
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILogger<ConnectionCacheResponseService> _logger;

        public ConnectionCacheResponseService(
            IRoutedItemManager routedItemManager,
            ILogger<ConnectionCacheResponseService> logger)
        {
            _routedItemManager = routedItemManager;
            _logger = logger;
        }

        /// <summary>
        ///  result cache where we consolidate multiple routed items based on the same id.The result is added onto the id: key
        ///  which usually arrives in chronological order(Pending, Pending, Success) though not required
        /// </summary>
        /// <param name="Connection"></param>
        /// <param name="routedItem"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        public RoutedItem CacheResponse(Connection Connection, RoutedItem routedItem, Dictionary<string, List<RoutedItem>> cache)
        {
            Throw.IfNull(Connection);

            if (routedItem.id != null)
            {
                lock (cache)
                {
                    _logger.Log(LogLevel.Debug, $"incoming conn: {routedItem.fromConnection} id: {routedItem.id} InstanceID: {routedItem.InstanceID} status: {routedItem.status}");

                    _routedItemManager.Init(routedItem);
                    var riClone = (RoutedItem)_routedItemManager.Clone();

                    //                List<RoutedItem> list = new List<RoutedItem>() { routedItem };
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.EnqueueCache(Connection, cache, nameof(cache));

                    // //add id to cache if not present. id is the record id from the requesting sou
                    // if (cache.TryAdd(routedItem.id, list))
                    // {
                    //     if (Logger.logger.FileTraceLevel == "Verbose") Logger.logger.Log(TraceEventType.Verbose, $"id: {routedItem.id} added to cache");

                    // }
                    // else
                    // {

                    //     if (Logger.logger.FileTraceLevel == "Verbose") Logger.logger.Log(TraceEventType.Verbose, $"id: {routedItem.id} exists in cache, consolidating");

                    //     cache[routedItem.id].Add(routedItem);

                    // }

                    //print out cache entries for this id
                    // if (Logger.logger.FileTraceLevel == "Verbose")
                    // {
                    //     foreach (var ri in cache[routedItem.id])
                    //     {
                    //         Logger.logger.Log(TraceEventType.Verbose, $"current cache conn: {routedItem.fromConnection} id: {routedItem.id} InstanceID: {ri.InstanceID} status: {ri.status}");
                    //     }
                    // }


                    //create a set by connection, 
                    Dictionary<string, RoutedItem.Status> set = new Dictionary<string, RoutedItem.Status>();

                    foreach (var ri in cache[routedItem.id])
                    {
                        set.TryAdd(ri.fromConnection, ri.status);
                        if (ri.status == RoutedItem.Status.COMPLETED)
                        {
                            set[ri.fromConnection] = RoutedItem.Status.COMPLETED;
                        }

                        if (ri.status == RoutedItem.Status.FAILED)
                        {
                            set[ri.fromConnection] = RoutedItem.Status.FAILED;
                        }

                        //populate any previously known toConnections.  Use case is BOUR-85 hl7 determining route for dicom with same id(patientID+accession)
                        if (routedItem.type != RoutedItem.Type.RPC)
                        {
                            //route caching doesn't work yet for request framework since the same request and resulting response goes both directions!!!

                            foreach (var conn in ri.toConnections)
                            {
                                if (!riClone.toConnections.Contains(conn))
                                {
                                    _logger.Log(LogLevel.Debug, $"Adding cached destination {conn.connectionName} id: {routedItem.id}");
                                    riClone.toConnections.Add(conn);
                                }
                            }
                        }
                    }

                    //determine if each set is complete
                    var completed = set.Count(e => e.Value == RoutedItem.Status.COMPLETED);
                    var failed = set.Count(e => e.Value == RoutedItem.Status.FAILED);
                    var statusnew = set.Count(e => e.Value == RoutedItem.Status.NEW);
                    var pending = set.Count(e => e.Value == RoutedItem.Status.PENDING);
                    RoutedItem.Status statusOfSet = RoutedItem.Status.PENDING;
                    if (completed == set.Count)
                    {
                        statusOfSet = RoutedItem.Status.COMPLETED;
                    }
                    else if (completed + failed == set.Count)
                    {
                        statusOfSet = RoutedItem.Status.FAILED;
                    }

                    _logger.Log(LogLevel.Debug, $"id: {routedItem.id} set.Count: {set.Count} completed: {completed} failed: {failed} pending: {pending} new: {statusnew}");

                    if (statusOfSet == RoutedItem.Status.COMPLETED || statusOfSet == RoutedItem.Status.FAILED)
                    {
                        //we're done
                        _logger.Log(LogLevel.Information, $"id: {routedItem.id} status of set: {statusOfSet} ");

                        //now merge responses by connection
                        Dictionary<string, CloudTaskResults> merged = new Dictionary<string, CloudTaskResults>();

                        foreach (var ri in cache[routedItem.id])
                        {
                            var mergedElement = new CloudTaskResults
                            {
                                connectionName = ri.fromConnection,
                                results = ri.response,
                                accessionNumber = ri.AccessionNumber,
                                mrn = ri.PatientID
                            };

                            if (!merged.TryAdd(ri.fromConnection, mergedElement))
                            {
                                foreach (var response in ri.response)
                                {
                                    if (!merged[ri.fromConnection].results.Contains(response))
                                    {
                                        merged[ri.fromConnection].results.Add(response);
                                    }
                                }

                                if (mergedElement.accessionNumber == null)
                                    mergedElement.accessionNumber = ri.AccessionNumber;
                                if (mergedElement.mrn == null) mergedElement.mrn = ri.PatientID;
                            }
                        }

                        riClone.cloudTaskResults.AddRange(merged.Values);
                        riClone.status = statusOfSet;
                    }
                    else
                    {
                        //not all connections are done
                        _logger.Log(LogLevel.Information, $"id: {routedItem.id} Not Complete ");
                        riClone.status = RoutedItem.Status.PENDING;
                    }

                    _logger.Log(LogLevel.Debug, $"cache total count after: {cache.Count}");

                    return riClone;
                }
            }
            else
            {
                _logger.Log(LogLevel.Warning, $"Cannot cache RoutedItem because id is null: {routedItem.sourceFileName}");
                return routedItem;
            }
        }

        public void RemoveCachedItem(Connection Connection, RoutedItem routedItem, Dictionary<string, List<RoutedItem>> cache)
        {
            Throw.IfNull(Connection);
            Throw.IfNull(cache);

            try
            {
                _routedItemManager.Init(routedItem);
                _routedItemManager.DequeueCache(Connection, cache, nameof(cache), error: false);

                _logger.Log(LogLevel.Debug, $"{routedItem.id} removed from cache");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }
    }
}
