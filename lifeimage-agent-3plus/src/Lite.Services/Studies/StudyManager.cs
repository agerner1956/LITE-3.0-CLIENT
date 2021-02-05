using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Services.Connections;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace Lite.Services.Studies
{
    public interface IStudyManager
    {
        Task DownloadStudy(int taskID, ImagingStudy imagingStudy, LifeImageCloudConnection connection, IHttpManager httpManager);
        Task getStudies(int taskID, LifeImageCloudConnection connection, IHttpManager httpManager);
        void MergeStudies(RootObject newStudies, LifeImageCloudConnection connection);
    }

    public sealed class StudyManager : IStudyManager
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly IStudiesDownloadManager _studiesDownloadManager;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public StudyManager(
            ILiteHttpClient liteHttpClient,
            IStudiesDownloadManager studiesDownloadManager,
            ILITETask taskManager,
            ILogger<StudyManager> logger)
        {
            _liteHttpClient = liteHttpClient;
            _studiesDownloadManager = studiesDownloadManager;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task DownloadStudy(int taskID, ImagingStudy imagingStudy, LifeImageCloudConnection connection, IHttpManager httpManager)
        {
            var Connection = connection;
            var stopWatch = new Stopwatch();
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                stopWatch.Start();

                _logger.Log(LogLevel.Debug, $"{taskInfo} downloading study: {imagingStudy.uid} downloadStarted: {imagingStudy.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} downloadCompleted: {imagingStudy.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} attempts: {imagingStudy.attempts}");

                foreach (var series in imagingStudy?.series)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} checking series: {series.uid} downloadStarted: {series.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} downloadCompleted: {series.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} attempts: {series.attempts}");

                    if (series.downloadCompleted == DateTime.MinValue) //not completed
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} not completed.");

                        if (series.downloadStarted < DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)) //not attempted lately
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} not attempted lately.");

                            if (imagingStudy.series?.FindAll(e => e.attempts > Connection.maxAttempts).Count == 0) //not exceeded max attempts
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} not exceeded max attempts.");

                                var url = $"{imagingStudy.url}/series/{series.uid.Substring(8)}";
                                //if the tasklist already contains this series don't add it again
                                //equal is determined by the reference field only
                                //so in this case it is the imagingStudy.url
                                if (await _taskManager.CountByReference(url) == 0)
                                {
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} not in task list.");
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} selected for download downloadStarted: {series.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} downloadCompleted: {series.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} attempts: {series.attempts}");

                                    series.downloadStarted = DateTime.Now;
                                    series.attempts++;
                                    var newTaskID = _taskManager.NewTaskID();
                                    Task task = new Task(new Action(async () => await _studiesDownloadManager.wadoAsFileStream(connection: connection, newTaskID, httpManager: httpManager, study: imagingStudy, series: series)), _taskManager.cts.Token);
                                    await _taskManager.Start(newTaskID, task, $"{Connection.name}.Wado", url, isLongRunning: false);
                                }
                                else
                                {
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} in task list. Skipping.");
                                }
                            }
                            else
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} exceeded max attempts. Skipping.");
                            }
                        }
                        else
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} attempted lately. Skipping.");
                        }
                    }
                    else
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} series: {series.uid} completed. Skipping.");
                    }
                }

                stopWatch.Stop();
                _logger.Log(LogLevel.Information, $"{taskInfo} method level elapsed: {stopWatch.Elapsed} study: {imagingStudy.uid}");
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);                
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.downloadStudy");
            }
        }

        public async Task getStudies(int taskID, LifeImageCloudConnection connection, IHttpManager httpManager)
        {
            var Connection = connection;
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var httpClient = _liteHttpClient.GetClient(connection);
            try
            {
                //set the URL
                string studiesURL =
                   Connection.URL + "/api/agent/v1/studies?state=NEEDS_DOWNLOADING&lifeImageSummary=true"; //add summary 

                _logger.Log(LogLevel.Debug, $"{taskInfo} studiesURL: {studiesURL}");

                var cookies = _liteHttpClient.GetCookies(studiesURL);
                _logger.LogCookies(cookies, taskInfo);

                // issue the GET
                var task = httpClient.GetAsync(studiesURL);
                var response = await task;

                // output the result                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);
                _logger.Log(LogLevel.Debug, $"{taskInfo} response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        httpManager.loginNeeded = true;
                    }

                    _logger.Log(LogLevel.Warning, $"{taskInfo} {response.StatusCode} {response.ReasonPhrase}");

                    _liteHttpClient.DumpHttpClientDetails();
                }

                //2018-02-06 shb convert from stream to JSON and clean up any non UTF-8 that appears like it did
                // when receiving "contains invalid UTF8 bytes" exception
                var serializer = new DataContractJsonSerializer(typeof(RootObject));
                var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.UTF8);
                byte[] byteArray = Encoding.UTF8.GetBytes(streamReader.ReadToEnd());
                MemoryStream stream = new MemoryStream(byteArray);

                var newStudies = serializer.ReadObject(stream) as RootObject;
                MergeStudies(newStudies, Connection);

                if (Connection.studies != null && Connection.studies.ImagingStudy != null)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} studies.ImagingStudy.Count: {Connection.studies.ImagingStudy.Count}");
                    foreach (var imagingStudy in Connection.studies.ImagingStudy)
                    {
                        _logger.Log(LogLevel.Information,
                            $"{taskInfo} ImagingStudy.uid: {imagingStudy.uid} series:{imagingStudy.numberOfSeries} instances:{imagingStudy.numberOfInstances}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                //eat it for now
                _logger.Log(LogLevel.Warning, $"{taskInfo} {e.Message} {e.StackTrace}");
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} Inner Exception: {e.InnerException}");
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
                //throw e;                
            }
        }

        public void MergeStudies(RootObject newStudies, LifeImageCloudConnection Connection)
        {
            lock (Connection.studies)
            {
                if (Connection.studies == null || Connection.studies.ImagingStudy == null || Connection.studies.ImagingStudy.Count == 0)
                {
                    _logger.Log(LogLevel.Information, $"Replacing studies");

                    Connection.studies = newStudies;
                }
                else
                {
                    //take the new studies from cloud and merge with existing
                    foreach (var study in newStudies.ImagingStudy)
                    {
                        if (!Connection.studies.ImagingStudy.Exists(e => e.uid == study.uid))
                        {
                            _logger.Log(LogLevel.Information, $"Adding {study.uid}");
                            Connection.studies.ImagingStudy.Add(study);
                        }
                        else
                        {
                            _logger.Log(LogLevel.Information, $"Study already exists: {study.uid}");
                        }
                    }
                }
            }
        }
    }
}
