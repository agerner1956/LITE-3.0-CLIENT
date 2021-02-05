using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Services.Http;
using Lite.Services.Studies;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface IMarkDownloadCompleteService
    {
        Task markDownloadComplete(int taskID, LifeImageCloudConnection connection, IHttpManager httpManager);
    }

    public sealed class MarkDownloadCompleteService : IMarkDownloadCompleteService
    {
        private readonly IStudyManager _studyManager;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public MarkDownloadCompleteService(
            IStudyManager studyManager,
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<MarkDownloadCompleteService> logger)
        {
            _studyManager = studyManager;
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LifeImageCloudConnection Connection { get; private set; }

        // markDownloadComplete is used to remove an item that was in the /studies call
        public async Task markDownloadComplete(int taskID, LifeImageCloudConnection connection, IHttpManager httpManager)
        {
            Connection = connection;

            var httpClient = _liteHttpClient.GetClient(connection);

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            HttpResponseMessage response = null;

            try
            {
                //process the series that have completed                
                _logger.Log(LogLevel.Debug, $"{taskInfo} Processing Series Completion");
                try
                {
                    //loop through the studies and if a study has all of its series downloaded, then we can remove it and tell cloud we downloaded it
                    if (Connection.studies != null && Connection.studies.ImagingStudy != null)
                    {
                        foreach (var study in Connection.studies.ImagingStudy.ToList())
                        {
                            var remaining = new List<Series>();
                            bool seriesFail = false;
                            if (study.series != null)
                            {
                                remaining = study.series?.FindAll(e => e.downloadCompleted == DateTime.MinValue);
                                //var studyFail = study.attempts > maxAttempts; we aren't doing study LogLevel.
                                seriesFail = study.series?.FindAll(e => e.attempts > Connection.maxAttempts).Count > 0;
                                //var instanceFail = ins //the study object contains a list of series but the series object
                                //does not contain a list of instances.  So no marking and clearing at instance level yet.
                            }

                            if (remaining.Count == 0)
                            {
                                foreach (var series in study.series)
                                {
                                    _logger.Log(LogLevel.Debug,
                                        $"{taskInfo} study: {study.uid} series: {series.uid} started: {series.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} completed: {series.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} duration: {series.downloadCompleted - series.downloadStarted} attempts: {series.attempts}");
                                }

                                study.downloadCompleted = study.series.Max(e => e.downloadCompleted);
                                study.downloadStarted = study.series.FindAll(e => e.downloadStarted != null)
                                    .Min(e => e.downloadStarted);
                                _logger.Log(LogLevel.Information,
                                    $"{taskInfo} study download (complete): {study.uid} started: {study.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} completed: {study.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} duration: {study.downloadCompleted - study.downloadStarted} attempts: {study.attempts}");
                                Connection.markDownloadsComplete.Add(new string[] { study.url, "download-complete" });
                            }

                            if (seriesFail)
                            {
                                foreach (var series in study.series)
                                {
                                    _logger.Log(LogLevel.Debug,
                                        $"{taskInfo} study: {study.uid} series: {series.uid} started: {series.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} completed: {series.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} duration: {series.downloadCompleted - series.downloadStarted} attempts: {series.attempts}");
                                }

                                study.downloadCompleted = study.series.Max(e => e.downloadCompleted);
                                study.downloadStarted = study.series.FindAll(e => e.downloadStarted != null).Min(e => e.downloadStarted);
                                _logger.Log(LogLevel.Information, $"{taskInfo} study download (failed): {study.uid} started: {study.downloadStarted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} completed: {study.downloadCompleted.ToString("yyyy-MM-dd HH:mm:ss.ffff")} duration: {study.downloadCompleted - study.downloadStarted} attempts: {study.attempts}");
                                _logger.Log(LogLevel.Information, $"{taskInfo} Failing study: {study.url}");
                                Connection.markDownloadsComplete.Add(new string[] { study.url, "download-fail" });
                            }
                        }
                    }

                    foreach (var seriesObj in Connection.markSeriesComplete.ToArray())
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} new Series Complete: {seriesObj.uid}");

                        if (Connection.studies != null && Connection.studies.ImagingStudy != null)
                        {
                            foreach (var study in Connection.studies?.ImagingStudy)
                            {
                                foreach (var series in study.series)
                                {
                                    if (series.uid == seriesObj.uid)
                                    {
                                        if (series.downloadCompleted != null)
                                        {
                                            _logger.Log(LogLevel.Debug,
                                                $"{taskInfo} writing timestamps markSeriesComplete: {series.uid}");
                                            series.downloadCompleted = seriesObj.downloadCompleted;
                                            series.downloadStarted = seriesObj.downloadStarted;
                                            series.attempts = seriesObj.attempts;
                                        }
                                        else
                                        {
                                            _logger.Log(LogLevel.Debug,
                                                $"{taskInfo} series already marked as complete: {series.uid}");
                                            series.downloadCompleted = DateTime.Now;
                                            series.attempts = seriesObj.attempts;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Connection.markSeriesComplete.Clear();
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }

                foreach (var markinfo in Connection.markDownloadsComplete.ToList())
                {
                    try
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} marking: {markinfo[0]} {markinfo[1]}");

                        var stopWatch = new Stopwatch();
                        stopWatch.Start();

                        string markDownloadCompleteURL = markinfo[0] + "/" + markinfo[1];
                        _logger.Log(LogLevel.Debug, $"{taskInfo} markDownloadCompleteURL: {markDownloadCompleteURL}");

                        //set the form parameters
                        var nothingParams = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("nothing", "nothing"),
                        });

                        var cookies = _liteHttpClient.GetCookies(markDownloadCompleteURL);
                        _logger.LogCookies(cookies, taskInfo);

                        // issue the POST
                        var task = httpClient.PostAsync(markDownloadCompleteURL, nothingParams, _taskManager.cts.Token);
                        response = await task;

                        // output the result                                                
                        _logger.LogHttpResponseAndHeaders(response, taskInfo);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            _logger.Log(LogLevel.Warning, $"{taskInfo} {response.StatusCode} {markDownloadCompleteURL}");

                            _liteHttpClient.DumpHttpClientDetails();
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            httpManager.loginNeeded = true;
                        }

                        if (response.StatusCode == HttpStatusCode.OK ||
                            response.StatusCode == HttpStatusCode.NotFound)
                        {
                            _logger.Log(LogLevel.Information, $"{taskInfo} {response.StatusCode} {markDownloadCompleteURL}");

                            lock (Connection.studies)
                            {
                                Connection.studies.ImagingStudy.RemoveAll(e => e.url == markinfo[0]);
                            }

                            lock (Connection.markDownloadsComplete)
                            {
                                Connection.markDownloadsComplete.Remove(markinfo);
                            }
                        }

                        _logger.Log(LogLevel.Debug,
                            $"{taskInfo} response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                        stopWatch.Stop();
                        _logger.Log(LogLevel.Information, $"{taskInfo} elapsed: {stopWatch.Elapsed}");
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
                    }
                    catch (HttpRequestException e)
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} Exception: {e.Message} {e.StackTrace}");
                        if (e.InnerException != null)
                        {
                            _logger.Log(LogLevel.Warning, $"{taskInfo} Inner Exception: {e.InnerException}");
                        }

                        _liteHttpClient.DumpHttpClientDetails();
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, $"{taskInfo} markDownloadComplete failed");
                        _liteHttpClient.DumpHttpClientDetails();
                    }
                }

                _logger.Log(LogLevel.Debug, $"{taskInfo} Processing getStudies");

                await _studyManager.getStudies(taskID, connection, httpManager);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            finally
            {
                try
                {
                    _taskManager.Stop($"{Connection.name}.markDownloadComplete");
                    if (response != null) response.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }
            }
        }
    }
}
