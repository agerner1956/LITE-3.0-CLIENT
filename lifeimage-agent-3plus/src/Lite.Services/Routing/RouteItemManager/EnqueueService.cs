using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Guard;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lite.Services.Routing.RouteItemManager
{
    /// <summary>
    /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list
    /// </summary>
    public interface IEnqueueService
    {
        /// <summary>
        /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list
        /// </summary>
        void Enqueue(RoutedItem Item, Connection conn, ObservableCollection<RoutedItem> list, string queueName, bool copy = false);
    }

    public class EnqueueService : RouteItemHandler, IEnqueueService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;

        public EnqueueService(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILogger<EnqueueService> logger)
            : base(logger)
        {
            _profileStorage = profileStorage;
            _util = util;
        }

        /// <summary>
        /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="copy"></param>
        public void Enqueue(RoutedItem Item, Connection conn, ObservableCollection<RoutedItem> list, string queueName, bool copy = false)
        {
            Throw.IfNull(Item);

            var taskInfo = $"task: {Item.TaskID} connection: {conn.name}";
            string dir;
            Item.attempts = 0;  //attempts from prior stages needs to be cleared on an enqueue
            Item.lastAttempt = DateTime.MinValue;

            try
            {
                //move or copy the sourceFileName
                if (Item.sourceFileName != null) //sourceFileName can be null for RoutedItems that do not reference a file such as Requests (Q/R)
                {
                    dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + conn.name + Path.DirectorySeparatorChar + queueName;
                    Directory.CreateDirectory(dir);

                    var filename = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid();
                    if (Item.sourceFileName.EndsWith(".hl7"))
                    {
                        filename += ".hl7";
                    }

                    if (File.Exists(Item.sourceFileName))
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Setting last access time to now for {Item.sourceFileName} to avoid the purge.");
                        File.SetLastAccessTime(Item.sourceFileName, DateTime.Now);


                        if (Item.toConnections.Count <= 1 && !copy)  //the connection might be enqueuing to itself before rule eval so the toConnection isn't yet populated.  If so we can def move
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Moving {Item.sourceFileName} to {filename}");
                            File.Move(Item.sourceFileName, filename);
                            Item.sourceFileName = filename;
                        }
                        else
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Copying {Item.sourceFileName} to {filename}");
                            File.Copy(Item.sourceFileName, filename);
                            Item.sourceFileName = filename;
                        }
                    }
                    else
                    {
                        _logger.Log(LogLevel.Critical, $"{taskInfo} sourceFileName: {Item.sourceFileName} does not exist.  Cannot Route this request.");
                        return;
                    }
                }


                //serialize the routedItem metadata to disk

                JsonSerializerOptions settings = new JsonSerializerOptions
                {
                    //Formatting = Formatting.Indented
                };


                dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + conn.name + Path.DirectorySeparatorChar + queueName + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
                Directory.CreateDirectory(dir);
                string fileName = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid() + Constants.Extensions.MetaExt;
                Item.RoutedItemMetaFile = fileName;

                string json = JsonSerializer.Serialize(Item, settings);

                if (string.IsNullOrEmpty(json))
                {
                    throw new Exception("json is empty or null");
                }

                if (!_util.IsDiskAvailable(fileName, _profileStorage.Current, json.Length))
                {
                    throw new Exception($"Insufficient disk to write {fileName}");
                }

                File.WriteAllText(fileName, json);
            }
            catch (Exception e)
            {
                WriteDetailedLog(e, Item, taskInfo);
            }

            try
            {
                lock (list)
                {
                    switch (Item.priority)
                    {
                        case Priority.Low:
                            list.Add(Item);  //to the end of the line you go
                            break;
                        case Priority.Medium:
                            list.Insert(list.Count / 2, Item);  //okay this is a hack.  We maybe should place above first found low priority. 
                            break;
                        case Priority.High:
                            list.Prepend(Item); //this should probably go above first found medium or low priority.
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                WriteDetailedLog(e, Item, taskInfo);
            }
        }
    }
}
