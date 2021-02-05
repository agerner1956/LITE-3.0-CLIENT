using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections.Files.Features;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Files
{
    public interface IFileConnectionManager : IConnectionManager
    {
    }

    public class FileConnectionManager : ConnectionManager<FileConnection>, IFileConnectionManager
    {
        [NonSerialized()]
        private readonly SemaphoreSlim ScanPathSignal = new SemaphoreSlim(0, 1);

        private readonly IFileScanService _fileScanService;
        private readonly IDuplicatesDetectionService _duplicatesDetectionService;
        private readonly IFilePathFormatterHelper _pathFormatterHelper;

        public FileConnectionManager(
            IFileScanService fileScanService,
            IDuplicatesDetectionService duplicatesDetectionService,
            IFilePathFormatterHelper pathFormatterHelper,
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRulesManager rulesManager,
            IUtil util,
            ILITETask taskManager,
            ILogger<FileConnectionManager> logger)
            : base(profileStorage, liteConfigService, routedItemManager, null, rulesManager, taskManager, logger, util)
        {
            _fileScanService = fileScanService;
            _pathFormatterHelper = pathFormatterHelper;
            _duplicatesDetectionService = duplicatesDetectionService;
        }

        public override void Init()
        {
            if (Connection.scanpaths != null && Connection.scanpaths.Count > 0)
            {
                foreach (var folder in Connection.scanpaths)
                {
                    WatchFolder(folder, "*.*", ScanPathSignal);
                }
            }

            Connection.started = true;
        }

        public override void Stop()
        {
            Connection.started = false;
        }

        public override async Task Kickoff(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Check for scanpaths");

                if (Connection.scanpaths?.Count > 0 && (LITETask.CanStart($"{Connection.name}.scan")))
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} scanpaths.count: {Connection.scanpaths.Count}");
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await Scan(newTaskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.scan", isLongRunning: false);
                }

                await Task.Yield(); //bogus await to make the sync go away
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);

                //throw e;
                throw;
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Kickoff");
            }
        }

        public string PathFormatter(RoutedItem routedItem)
        {
            return _pathFormatterHelper.PathFormatter(routedItem, Connection);
        }

        // Incoming file, store
        public override RoutedItem Route(RoutedItem routedItem, bool copy = false)
        {
            var taskInfo = $"task: {routedItem.TaskID} connection: {Connection.name}";

            //enqueue the routedItem, we don't support Q/R at this stage of dev
            if (routedItem.sourceFileName != null)
            {
                DirectoryInfo mydir;

                mydir = Directory.CreateDirectory(PathFormatter(routedItem));

                _logger.Log(LogLevel.Debug,
                    $"{taskInfo} Creating File: {Connection.inpath}/{routedItem.sourceFileName.Replace(":", "-")}");

                try
                {
                    routedItem.destFileName = mydir.FullName + Path.DirectorySeparatorChar + routedItem.sourceFileName
                        .Substring(routedItem.sourceFileName.LastIndexOf(
                            Path.DirectorySeparatorChar) + 1).Replace(":", "-");
                    bool result = true;
                    if (_profileStorage.Current.duplicatesDetectionUpload && routedItem.type == RoutedItem.Type.DICOM)
                    {
                        var dd = _duplicatesDetectionService;
                        dd.DuplicatesPurge();
                        lock (routedItem)
                        {
                            if (!dd.DuplicatesReference(routedItem.fromConnection + "_FileConnection",
                                routedItem.sourceFileName))
                            {
                                result = false;
                            }
                        }
                    }

                    if (result)
                    {
                        File.Copy(routedItem.sourceFileName, routedItem.destFileName, true);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }
            }

            return routedItem;
        }

        /// <summary>
        /// Outgoing file, send.
        /// </summary>
        /// <param name="taskID"></param>
        /// <returns></returns>
        public async Task Scan(int taskID)
        {
            await _fileScanService.Scan(taskID, Connection, this, ScanPathSignal);
        }

        [Obsolete]
        public async Task SendFile(string filePath, int taskID)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ScanPathSignal.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool WatchFolder(string path, string pattern, SemaphoreSlim signal)
        {
            FileSystemWatcher watcher = new FileSystemWatcher();

            Directory.CreateDirectory(path);
            watcher.Path = path;
            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            watcher.NotifyFilter = NotifyFilters.LastAccess
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            // Only watch pattern files.
            watcher.Filter = pattern;

            // Add event handlers.
            //watcher.Changed += method;
            watcher.Created += (object source, FileSystemEventArgs e) =>
            {
                Console.WriteLine($"File: {e.FullPath} {e.ChangeType} {e.Name}");
                // Signal event to wake up and process if awaiting data
                try
                {
                    if (signal != null && signal.CurrentCount == 0) signal.Release();
                }
                catch (Exception)
                {
                } //could be in the middle of being disposed and recreated
            };
            //watcher.Deleted += method;
            //watcher.Renamed += method;
            watcher.Error += (object source, ErrorEventArgs e) =>
            {
                // Specify what is done when a file is changed, created, or deleted.
                Console.WriteLine($"File: {e.GetType()} {e.ToString()} {e.GetException()}");
            };
            // Begin watching.
            watcher.EnableRaisingEvents = true;

            return FileSystemWatchers.TryAdd(path, watcher);
        }
    }
}
