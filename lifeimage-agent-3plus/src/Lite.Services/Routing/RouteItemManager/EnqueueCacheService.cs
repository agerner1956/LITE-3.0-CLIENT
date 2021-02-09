using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lite.Services.Routing.RouteItemManager
{
    /// <summary>
    /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list.
    /// </summary>
    public interface IEnqueueCacheService
    {
        /// <summary>
        /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="copy"></param>
        void EnqueueCache(RoutedItem Item, Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool copy = true);
    }

    public sealed class EnqueueCacheService : RouteItemHandler, IEnqueueCacheService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;

        public EnqueueCacheService(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILogger<EnqueueCacheService> logger) : base(logger)
        {
            _profileStorage = profileStorage;
            _util = util;
        }

        public void EnqueueCache(RoutedItem Item, Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool copy = true)
        {
            Throw.IfNull(Item);

            var taskInfo = $"task: {Item.TaskID} connection: {conn.name}";
            string dir;
            Item.attempts = 0;  //attempts from prior stages needs to be cleared on an enqueue
            Item.lastAttempt = DateTime.MinValue;

            try
            {
                if (Item.id != null)
                {
                    lock (list)
                    {
                        //add id to cache if not present. 
                        List<RoutedItem> cacheEntry = new List<RoutedItem>() { Item };

                        if (list.TryAdd(Item.id, cacheEntry))
                        {
                            try
                            {
                                //serialize the cache entry to disk

                                // old code
                                //JsonSerializerSettings settings = new JsonSerializerSettings
                                //{
                                //    Formatting = Formatting.Indented
                                //};

                                JsonSerializerOptions settings = new JsonSerializerOptions
                                {

                                };

                                dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Constants.Dirs.ResponseCache + Path.DirectorySeparatorChar + queueName + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
                                Directory.CreateDirectory(dir);
                                string fileName = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid() + Constants.Extensions.MetaExt;
                                Item.RoutedItemMetaFile = fileName;

                                string json = JsonSerializer.Serialize(Item, settings); //list[id]

                                if (string.IsNullOrEmpty(json))
                                {
                                    throw new Exception("json is empty or null");
                                }

                                if (!_util.IsDiskAvailable(fileName, _profileStorage.Current, json.Length))
                                {
                                    throw new Exception($"Insufficient disk to write {fileName}");
                                }

                                File.WriteAllText(fileName, json);
                                _logger.Log(LogLevel.Debug, $"id: {Item.id} added to {queueName} file: {fileName} ");
                            }
                            catch (Exception e)
                            {
                                WriteDetailedLog(e, Item, taskInfo);
                            }
                        }
                        else
                        {
                            //we need to figure out whether this is a duplicate from route caching, ie it's not a bi-directional request
                            var existing = list[Item.id].Find(e => e.fromConnection == Item.fromConnection);
                            if (existing == null)
                            {
                                list[Item.id].Add(Item);
                                try
                                {
                                    //serialize the cache entry to disk

                                    JsonSerializerOptions settings = new JsonSerializerOptions
                                    {
                                        //Formatting = Formatting.Indented
                                    };

                                    dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Constants.Dirs.ResponseCache + Path.DirectorySeparatorChar + queueName + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
                                    Directory.CreateDirectory(dir);
                                    string fileName = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid() + Constants.Extensions.MetaExt;
                                    Item.RoutedItemMetaFile = fileName;

                                    string json = JsonSerializer.Serialize(Item, settings); //list[id]

                                    if (string.IsNullOrEmpty(json))
                                    {
                                        throw new Exception("json is empty or null");
                                    }

                                    if (!_util.IsDiskAvailable(fileName, _profileStorage.Current, json.Length))
                                    {
                                        throw new Exception($"Insufficient disk to write {fileName}");
                                    }

                                    File.WriteAllText(fileName, json);
                                    _logger.Log(LogLevel.Debug, $"id: {Item.id} merged to {queueName} file: {fileName} ");
                                }
                                catch (Exception e)
                                {
                                    WriteDetailedLog(e, Item, taskInfo);
                                }
                            }
                            else
                            {
                                if (Item.toConnections.Count > 0)
                                {
                                    existing.toConnections = Item.toConnections;
                                }

                                if (Item.status == RoutedItem.Status.COMPLETED || Item.status == RoutedItem.Status.FAILED)
                                {
                                    //add the responses
                                    existing.status = Item.status;
                                    foreach (var response in Item.response.ToArray())
                                    {
                                        if (!existing.response.Contains(response))
                                        {
                                            existing.response.Add(response);
                                        }
                                    }

                                    foreach (var kvp in Item.TagData.ToArray())
                                    {
                                        existing.TagData.TryAdd(kvp.Key, kvp.Value);
                                    }

                                    foreach (var result in Item.cloudTaskResults.ToArray())
                                    {
                                        if (!existing.cloudTaskResults.Contains(result))
                                        {
                                            existing.cloudTaskResults.Add(result);
                                        }
                                    }
                                }

                                try
                                {
                                    //serialize the cache entry to disk using existing filename

                                    JsonSerializerOptions settings = new JsonSerializerOptions
                                    {
                                        //Formatting = Formatting.Indented
                                    };

                                    string json = JsonSerializer.Serialize(existing, settings); //list[id]

                                    if (string.IsNullOrEmpty(json))
                                    {
                                        throw new Exception("json is empty or null");
                                    }

                                    if (!_util.IsDiskAvailable(existing.RoutedItemMetaFile, _profileStorage.Current, json.Length))
                                    {
                                        throw new Exception($"Insufficient disk to write {existing.RoutedItemMetaFile}");
                                    }

                                    File.WriteAllText(existing.RoutedItemMetaFile, json);
                                    _logger.Log(LogLevel.Debug, $"id: {Item.id} merged to {queueName} file: {existing.RoutedItemMetaFile} ");
                                }
                                catch (Exception e)
                                {
                                    WriteDetailedLog(e, Item, taskInfo);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} Cannot cache RoutedItem because id is null: {Item.sourceFileName}");
                }
            }
            catch (Exception e)
            {
                WriteDetailedLog(e, Item, taskInfo);
            }
        }
    }
}
