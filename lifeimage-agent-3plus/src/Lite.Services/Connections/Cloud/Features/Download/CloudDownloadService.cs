using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Services.Studies;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ICloudDownloadService
    {
        Task Download(int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager);
    }

    public sealed class CloudDownloadService : ICloudDownloadService
    {        
        private readonly IProfileStorage _profileStorage;
        private readonly IDuplicatesDetectionService _duplicatesDetectionService;
        private readonly IStudyManager _studyManager;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public CloudDownloadService(
            IProfileStorage profileStorage,
            IDuplicatesDetectionService duplicatesDetectionService,
            IStudyManager studyManager,
            ILITETask taskManager,
            ILogger<CloudDownloadService> logger
            )
        {
            _profileStorage = profileStorage;
            _duplicatesDetectionService = duplicatesDetectionService;
            _studyManager = studyManager;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task Download(int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager)
        {            
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering Download");

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Processing downloadStudy");

                //we can kick off some more
                //be careful that this is reentrant, meaning that kickoff launches this on an interval and we only want to start
                //new work if existing work to capacity is not already occurring.

                //to avoid Collection was modified; enumeration operation may not execute, making a copy just to iterate.

                await DownloadImpl(Connection, httpManager, taskInfo);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} Exiting Download");
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.Download");
            }
        }

        private async Task DownloadImpl(LifeImageCloudConnection Connection, IHttpManager httpManager, string taskInfo)
        {            
            //to avoid Collection was modified; enumeration operation may not execute, making a copy just to iterate.

            if (Connection.studies == null || Connection.studies.ImagingStudy == null)
            {
                return;
            }

            //var copyOfStudies = studies.ImagingStudy.ToList();
            if (_profileStorage.Current.duplicatesDetectionDownload)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo}: {Connection.studies.ImagingStudy.Count} studies to download (before duplicates elimination).");
            }
            else
            {
                _logger.Log(LogLevel.Information, $"{taskInfo}: {Connection.studies.ImagingStudy.Count} studies to download.");
            }

            foreach (var imagingStudy in Connection.studies.ImagingStudy)
            {
                await ProcessImageStudy(Connection, imagingStudy, httpManager, taskInfo);
            }
        }

        private async Task<bool> ProcessImageStudy(LifeImageCloudConnection Connection, ImagingStudy imagingStudy, IHttpManager httpManager, string taskInfo)
        {            
            string duplicatesDirName = Connection.name;

            if (_profileStorage.Current.duplicatesDetectionDownload)
            {
                _duplicatesDetectionService.DuplicatesPurge();
                lock (imagingStudy)
                {
                    if (!_duplicatesDetectionService.DuplicatesReference1(duplicatesDirName, imagingStudy.uid))
                    {
                        //studies.ImagingStudy.Remove(imagingStudy);
                        return false;
                    }
                }
            }

            _logger.Log(LogLevel.Information, $"{taskInfo} checking study: {imagingStudy.uid} downloadStarted:{imagingStudy.downloadStarted:yyyy-MM-dd HH:mm:ss.ffff} downloadCompleted:{imagingStudy.downloadCompleted:yyyy-MM-dd HH:mm:ss.ffff} attempts: {imagingStudy.attempts} seriesOverMaxAttempts:{imagingStudy.series?.FindAll(e => e.attempts > Connection.maxAttempts).Count}");

            if (await _taskManager.CountByReference(imagingStudy.uid) != 0) //not in task
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} study: {imagingStudy.uid} in current tasks. Skipping.");                
                return false;
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} study: {imagingStudy.uid} not in current tasks.");

            if (imagingStudy.downloadCompleted != DateTime.MinValue) //not completed
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} study: {imagingStudy.uid} completed. Skipping.");
                return false;                
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} study: {imagingStudy.uid} not completed.");

            if (imagingStudy.downloadStarted >= DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)) //not attempted lately
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} study: {imagingStudy.uid} attempted lately. Skipping.");
                return false;
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} study: {imagingStudy.uid} not attempted lately.");

            if ((imagingStudy.series?.FindAll(e => e.attempts > Connection.maxAttempts).Count) != 0) //not exceeded max attempts
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} study: {imagingStudy.uid} has exceeded max attempts. Skipping.");
                return false;
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} study: {imagingStudy.uid} has not exceeded max attempts.");

            _logger.Log(LogLevel.Information, $"{taskInfo} study: {imagingStudy.uid} attempts: {imagingStudy.attempts} selected for download.");
            imagingStudy.downloadStarted = DateTime.Now;
            imagingStudy.attempts++;

            var newTaskID = _taskManager.NewTaskID();
            Task task = new Task(new Action(async () => await _studyManager.DownloadStudy(newTaskID, imagingStudy, Connection, httpManager)), _taskManager.cts.Token);
            await _taskManager.Start(newTaskID, task, $"{Connection.name}.downloadStudy", $"{imagingStudy.uid}", isLongRunning: false);

            return true;
        }
    }
}
