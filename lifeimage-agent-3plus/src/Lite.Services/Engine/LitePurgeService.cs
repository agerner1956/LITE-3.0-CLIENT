using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LitePurgeService : ILitePurgeService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDiskUtils _util;
        private readonly ILogger _logger;
        private readonly ILITETask _taskManager;

        public LitePurgeService(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILITETask taskManager,
            ILogger<LitePurgeService> logger)
        {
            _profileStorage = profileStorage;
            _util = util;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task Purge(int taskID)
        {
            var profile = _profileStorage.Current;

            var taskInfo = $"task: {taskID}";

            try
            {
                //purge logs
                //                 try
                //                 {
                //                     if (Logger.logger.FileTraceLevel == "Verbose") Logger.logger.Log(TraceEventType.Verbose, $"{taskInfo} Purging Logs");
                //                     var date = DateTime.Now.AddDays(profile.logRetentionDays * -1);
                // //                    var logFileCleanup = new LogFileCleanup();
                // //                    logFileCleanup.CleanUp(date);
                //                 }
                //                 catch (Exception e)
                //                 {
                //                     Logger.logger.Log(TraceEventType.Error, $"Log Purge Failed. {e.Message} {e.StackTrace}");
                //                     if (e.InnerException != null)
                //                     {
                //                         Logger.logger.Log(TraceEventType.Warning, $"Inner Exception: {e.InnerException}");
                //                     }
                //                 }

                await Task.Run(() =>
                {
                    //purge temp
                    Directory.CreateDirectory(profile.tempPath);
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Purging {profile.tempPath}");

                    foreach (var file in _util.DirSearch(profile.tempPath, "*.*"))
                    {
                        try
                        {
                            if (File.Exists(file))
                            {
                                var attr = File.GetAttributes(file);
                                if (!attr.HasFlag(FileAttributes.Hidden))
                                {
                                    var lastaccesstime = File.GetLastAccessTime(file);
                                    var creationtime = File.GetCreationTime(file);
                                    var lastwritetime = File.GetLastWriteTime(file);
                                    var purgetime = DateTime.Now.AddHours(profile.tempFileRetentionHours * -1);
                                    if (lastwritetime.CompareTo(purgetime) < 0
                                        && lastaccesstime.CompareTo(purgetime) < 0
                                        && creationtime.CompareTo(purgetime) < 0)
                                    {
                                        _logger.Log(LogLevel.Debug, $"Purging: {file}");

                                        try
                                        {
                                            File.Delete(file);
                                        }
                                        catch (Exception e)
                                        {
                                            _logger.LogFullException(e, taskInfo);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} Deleting hidden file {file}.");
                                    try
                                    {
                                        File.Delete(file); //delete hidden files like .DS_Store
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogFullException(e, taskInfo);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogFullException(e);
                        }
                    }

                    _util.CleanUpDirectory(profile.tempPath);
                });
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
                _taskManager.Stop($"Purge");
            }
        }
    }
}
