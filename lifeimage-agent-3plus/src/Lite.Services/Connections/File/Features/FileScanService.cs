using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Files.Features
{
    public interface IFileScanService
    {
        Task Scan(int taskID, FileConnection fileConnection, IConnectionRoutedCacheManager connectionManager, SemaphoreSlim scanPathSignal);
    }

    public sealed class FileScanService : IFileScanService
    {
        private readonly IDuplicatesDetectionService _duplicatesDetectionService;
        private readonly ISendFileService _sendFileService;
        private readonly IProfileStorage _profileStorage;
        private readonly IFileExpanderService _fileExpanderService;
        private readonly IDiskUtils _util;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public FileScanService(
            IDuplicatesDetectionService duplicatesDetectionService,
            ISendFileService sendFileService,
            IProfileStorage profileStorage,
            IFileExpanderService fileExpanderService,
            IDiskUtils util,
            ILITETask taskManager,
            ILogger<FileScanService> logger)
        {
            _duplicatesDetectionService = duplicatesDetectionService;
            _sendFileService = sendFileService;
            _profileStorage = profileStorage;
            _fileExpanderService = fileExpanderService;
            _util = util;
            _taskManager = taskManager;
            _logger = logger;
        }

        public FileConnection Connection { get; set; }

        /// <summary>
        /// Outgoing file, send.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="fileConnection"></param>
        /// <param name="connectionManager"></param>
        /// <param name="ScanPathSignal"></param>
        /// <returns></returns>
        public async Task Scan(int taskID, FileConnection fileConnection, IConnectionRoutedCacheManager connectionManager, SemaphoreSlim ScanPathSignal)
        {
            Connection = fileConnection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} scanpaths: {Connection.scanpaths}");

            try
            {
                do
                {
                    bool success = await ScanPathSignal.WaitAsync(_profileStorage.Current.KickOffInterval, _taskManager.cts.Token)
                        .ConfigureAwait(false);
                    // ScanPathSignal.Dispose();
                    // ScanPathSignal = new SemaphoreSlim(0, 1);


                    foreach (string fileEntry in Connection.scanpaths)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} fileEntry: {fileEntry}");
                        // get the file attributes for file or directory
                        //            FileAttributes attr = File.GetAttributes(fileEntry);

                        string directory = null;
                        string searchPattern = null;
                        string filestr = null; //needed because can't use fileEntry during home dir expansion

                        //Expand home dir syntax ex: ~/blah
                        filestr = _fileExpanderService.Expand(fileEntry);

                        _logger.Log(LogLevel.Debug, $"{taskInfo} expanded: {filestr}");

                        directory = filestr;

                        _logger.Log(LogLevel.Debug, $"{taskInfo} create directory: {directory}");

                        var directoryFullPath = Path.GetDirectoryName(directory);
                        Directory.CreateDirectory(directoryFullPath); //This may cause problems if we are specifying files instead of directories.
                        Directory.CreateDirectory(directory); //This may cause problems if we are specifying files instead of directories.

                        if (filestr.IndexOf("*") != -1)
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} wildcard search pattern detected.");

                            directory = Path.GetDirectoryName(filestr);
                            searchPattern = Path.GetFileName(filestr);
                        }
                        else
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} using search pattern *.");
                            searchPattern = "*";
                        }

                        FileAttributes attr = File.GetAttributes(directory);

                        if (attr.HasFlag(FileAttributes.Directory))
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} This is a directory.");

                            var fileEntries = _util.DirSearch(directory, searchPattern);
                            var dd = _duplicatesDetectionService;
                            if (_profileStorage.Current.duplicatesDetectionUpload)
                            {
                                _logger.Log(LogLevel.Information,
                                    $"{taskInfo} toCloud: {(fileEntries == null ? 0 : fileEntries.Count)} files to be send to cloud (before duplicates elimination).");
                            }
                            else
                            {
                                _logger.Log(LogLevel.Information,
                                    $"{taskInfo} toCloud: {(fileEntries == null ? 0 : fileEntries.Count)} files to be send to cloud.");
                            }

                            foreach (string file in fileEntries)
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Found {file}.");

                                try
                                {
                                    attr = File.GetAttributes(file);
                                    if (!attr.HasFlag(FileAttributes.Hidden))
                                    {
                                        var newTaskID = _taskManager.NewTaskID();
                                        await _sendFileService.SendFile(file, newTaskID, Connection, connectionManager);
                                    }
                                    else
                                    {
                                        _logger.Log(LogLevel.Debug, $"{taskInfo} Deleting hidden file {file}.");
                                        File.Delete(file); //delete hidden files like .DS_Store
                                    }
                                }
                                catch (System.IO.FileNotFoundException e)
                                {
                                    _logger.Log(LogLevel.Debug, $"Retrieve Exception: {e.Message} {e.StackTrace}");
                                    //eat it if not found, it's already gone
                                }
                            }

                            //cleanup empty directories.                            
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Cleaning Empty Directories {directory}.");

                            _util.CleanUpDirectory(directory);
                        }
                        else
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Sending individually specified {filestr}.");
                            await _sendFileService.SendFile(filestr, taskID, Connection, connectionManager);
                        }

                        await Task.Yield();
                    }

                    //purge inpath 
                    if ((Connection.inout == InOut.both || Connection.inout == InOut.inbound) && Connection.inpath != null)
                    {
                        Directory.CreateDirectory(Connection.inpath);

                        _logger.Log(LogLevel.Debug, $"{taskInfo} Purging inpath: {Connection.inpath}");

                        foreach (var file in _util.DirSearch(Connection.inpath, "*.*"))
                        {
                            var attr = File.GetAttributes(file);
                            if (!attr.HasFlag(FileAttributes.Hidden))
                            {
                                var lastwritetime = File.GetLastWriteTime(file);
                                var lastaccesstime = File.GetLastAccessTime(file);
                                var creationtime = File.GetCreationTime(file);

                                var purgetime = DateTime.Now.AddHours(Connection.inpathRetentionHours * -1);
                                if (lastwritetime.CompareTo(purgetime) < 0
                                    && lastaccesstime.CompareTo(purgetime) < 0
                                    && creationtime.CompareTo(purgetime) < 0)
                                {
                                    _logger.Log(LogLevel.Debug, $"Purging: {file}");
                                    File.Delete(file);
                                }
                            }
                            else
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Deleting hidden file {file}.");
                                File.Delete(file); //delete hidden files like .DS_Store
                            }
                        }

                        _util.CleanUpDirectory(Connection.inpath);
                    }

                    //purge outpath 
                    if ((Connection.inout == InOut.both || Connection.inout == InOut.outbound) && Connection.outpath != null)
                    {
                        Directory.CreateDirectory(Connection.outpath);
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Purging outpath: {Connection.outpath}");
                        foreach (var file in _util.DirSearch(Connection.outpath, "*.*"))
                        {
                            var attr = File.GetAttributes(file);
                            if (!attr.HasFlag(FileAttributes.Hidden))
                            {
                                var lastwritetime = File.GetLastWriteTime(file);
                                var purgetime = DateTime.Now.AddHours(Connection.outpathRetentionHours * -1);
                                if (lastwritetime.CompareTo(purgetime) < 0)
                                {
                                    _logger.Log(LogLevel.Debug, $"Purging: {file}");
                                    File.Delete(file);
                                }
                            }
                            else
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Deleting hidden file {file}.");
                                File.Delete(file); //delete hidden files like .DS_Store
                            }
                        }

                        _util.CleanUpDirectory(Connection.outpath);
                    }
                } while (Connection.responsive);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.scan");
            }
        }
    }
}
