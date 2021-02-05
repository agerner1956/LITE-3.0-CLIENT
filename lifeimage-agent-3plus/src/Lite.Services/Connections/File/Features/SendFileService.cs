using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Files.Features
{
    public interface ISendFileService
    {
        Task SendFile(string filePath, int taskID, FileConnection connection, IConnectionRoutedCacheManager connectionManager);
    }

    public class SendFileService : ISendFileService
    {
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IRulesManager _rulesManager;
        private readonly IFileExpanderService _fileExpanderService;
        private readonly ILogger _logger;

        public SendFileService(
            IFileExpanderService fileExpanderService,
            IRoutedItemManager routedItemManager,
            IRulesManager rulesManager,
            ILogger<SendFileService> logger)
        {
            _rulesManager = rulesManager;
            _fileExpanderService = fileExpanderService;
            _routedItemManager = routedItemManager;
            _logger = logger;
        }

        public FileConnection Connection { get; set; }

        public async Task SendFile(string filePath, int taskID, FileConnection connection, IConnectionRoutedCacheManager connectionManager)
        {
            Connection = connection;
            _logger.Log(LogLevel.Debug, $"Sending {filePath}");

            RoutedItem routedItem = new RoutedItem(fromConnection: Connection.name, sourceFileName: filePath, taskID: taskID)
            {
                type = RoutedItem.Type.FILE
            };

            try
            {
                routedItem.type = RoutedItem.Type.FILE;
                _routedItemManager.Init(routedItem);
                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                await _rulesManager.SendToRules(routedItem, _routedItemManager, connectionManager);
                //await LITE.profile.rules.SendToRules(routedItem);

                _routedItemManager.Dequeue(Connection, Connection.toRules, nameof(Connection.toRules));

                if (Connection.outpath != null && File.Exists(filePath))
                {
                    var expandedOutPath = _fileExpanderService.Expand(Connection.outpath);
                    // collapse the duplicate part of the path, prob more simple way of thinking about this.
                    string dir =
                        $"{expandedOutPath}{filePath.Substring(0, filePath.LastIndexOf(Path.DirectorySeparatorChar)).Replace(_fileExpanderService.Expand("~"), "")}";
                    string file = $"{expandedOutPath}{filePath.Replace(_fileExpanderService.Expand("~"), "")}";

                    _logger.Log(LogLevel.Debug, $"Moving {filePath} to {file}");
                    Directory.CreateDirectory(dir);

                    //await Task.Yield();

                    if (File.Exists(file))
                    {
                        var orgFileDateTime = File.GetLastWriteTime(file).ToString()
                            .Replace(Path.DirectorySeparatorChar, '-').Replace(":", "-");
                        var destinationBackupFileName = $"{file}.{orgFileDateTime}";

                        // Remove the file to guarantee the move works
                        //File.Delete(file);   
                        File.Replace(filePath, file, destinationBackupFileName, true);
                    }
                    else
                    {
                        File.Move(filePath, file);
                    }
                    _logger.Log(LogLevel.Debug, $"Moved {filePath} to {file}");
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"outpath is null.");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);

                _routedItemManager.Init(routedItem);
                _routedItemManager.Dequeue(Connection, Connection.toRules, nameof(Connection.toRules), error: true);
            }
        }
    }
}
