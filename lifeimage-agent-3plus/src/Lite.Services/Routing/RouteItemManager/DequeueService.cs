using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;

namespace Lite.Services.Routing.RouteItemManager
{
    /// <summary>
    /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
    /// </summary>
    public interface IDequeueService
    {
        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        void Dequeue(RoutedItem Item, Connection conn, string queueName, bool error = false, Stream stream = null);
    }

    public sealed class DequeueService : RouteItemHandler, IDequeueService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;

        public DequeueService(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILogger<DequeueService> logger) : base(logger)
        {
            _profileStorage = profileStorage;
            _util = util;
        }

        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="conn"></param>
        /// <param name="queueName"></param>
        /// <param name="error"></param>
        /// <param name="stream"></param>
        public void Dequeue(RoutedItem Item, Connection conn, string queueName, bool error = false, Stream stream = null)
        {
            var taskInfo = $"task: {Item.TaskID} connection: {conn.name}";

            Item.lastAttempt = DateTime.MaxValue;

            var dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + conn.name + Path.DirectorySeparatorChar + queueName + Path.DirectorySeparatorChar + Constants.Dirs.Errors;
            Directory.CreateDirectory(dir);
            string metadir = dir + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
            Directory.CreateDirectory(metadir);

            var fileName = Guid.NewGuid().ToString();
            string metafileName = metadir + Path.DirectorySeparatorChar + fileName + Constants.Extensions.MetaExt;
            fileName = dir + Path.DirectorySeparatorChar + fileName;

            _logger.Log(LogLevel.Debug, $"{taskInfo} Dequeue meta: {Item.RoutedItemMetaFile} source: {Item.sourceFileName} error: {error}");

            if (stream != null)
            {
                stream.Dispose();
            }

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

                // move source file

                try
                {
                    if (Item.sourceFileName != null & File.Exists(Item.sourceFileName))
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Move: {Item.sourceFileName} to: {fileName}");
                        File.Move(Item.sourceFileName, fileName);
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

                // remove source file

                try
                {
                    if (Item.sourceFileName != null & File.Exists(Item.sourceFileName))
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Delete: {Item.sourceFileName}");
                        File.Delete(Item.sourceFileName);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, $"{taskInfo} routedItemMetaFile: {(Item.RoutedItemMetaFile ?? "null")}");
                }
            }
        }
    }
}
