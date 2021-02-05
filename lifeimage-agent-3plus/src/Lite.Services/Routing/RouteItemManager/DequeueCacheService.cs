using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lite.Services.Routing.RouteItemManager
{
    /// <summary>
    /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
    /// </summary>
    public interface IDequeueCacheService
    {
        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        void DequeueCache(RoutedItem Item, Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool error = false);
    }

    public sealed class DequeueCacheService : RouteItemHandler, IDequeueCacheService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;

        public DequeueCacheService(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILogger<DequeueCacheService> logger) : base(logger)
        {
            _profileStorage = profileStorage;
            _util = util;
        }

        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="error"></param>
        public void DequeueCache(RoutedItem Item, Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool error = false)
        {
            var taskInfo = $"task: {Item.TaskID} connection: {conn.name}";

            Item.lastAttempt = DateTime.MaxValue;

            var dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + "ResponseCache" + Path.DirectorySeparatorChar + queueName + Path.DirectorySeparatorChar + "errors";
            Directory.CreateDirectory(dir);
            string metadir = dir + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
            Directory.CreateDirectory(metadir);

            var fileName = Item.id;
            string metafileName = metadir + Path.DirectorySeparatorChar + fileName + Constants.Extensions.MetaExt;
            fileName = dir + Path.DirectorySeparatorChar + fileName;

            _logger.Log(LogLevel.Debug, $"{taskInfo} Dequeue meta: {Item.RoutedItemMetaFile} error: {error}");

            if (error)
            {
                //serialize the routedItem metadata to disk which should contain the error for diagnostics

                JsonSerializerOptions settings = new JsonSerializerOptions
                {
                    //Formatting = Formatting.Indented
                };

                string json = JsonSerializer.Serialize(Item, settings);

                if (string.IsNullOrEmpty(json))
                {
                    throw new Exception("json is empty or null");
                }

                if (!_util.IsDiskAvailable(fileName, _profileStorage.Current, json.Length))
                {
                    throw new Exception($"Insufficient disk to write {fileName}");
                }

                File.WriteAllText(Item.RoutedItemMetaFile, json);

                //move to errors
                // move the meta file
                try
                {
                    if (Item.RoutedItemMetaFile != null & File.Exists(Item.RoutedItemMetaFile))
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Move: {Item.RoutedItemMetaFile} to: {metafileName}");
                        File.Move(Item.RoutedItemMetaFile, metafileName);
                    }
                }
                catch (Exception e)
                {
                    WriteDetailedLog(e, Item, taskInfo);
                }
            }
            else
            {
                // remove metadata
                try
                {
                    if (Item.RoutedItemMetaFile != null & File.Exists(Item.RoutedItemMetaFile))
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Delete: {Item.RoutedItemMetaFile}");
                        File.Delete(Item.RoutedItemMetaFile);
                    }
                }
                catch (Exception e)
                {
                    WriteDetailedLog(e, Item, taskInfo);
                }
            }

            try
            {
                lock (list)
                {
                    if (list.ContainsKey(Item.id))
                    {
                        list.Remove(Item.id);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} routedItemMetaFile: {(metafileName ?? "null")}");
            }
        }
    }
    
}
