using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lite.Services.Studies
{
    public interface IStudiesDownloadManager
    {
        Task wadoAsFileStream(
            LifeImageCloudConnection connection,
            int taskID,
            ImagingStudy study,
            IHttpManager httpManager,
            Series series = null,
            Instance instance = null,
            bool compress = true);
    }

    public class StudiesDownloadManager : IStudiesDownloadManager
    {
        private readonly ILogger _logger;
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IDiskUtils _util;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;

        public StudiesDownloadManager(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IDiskUtils util,
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<StudiesDownloadManager> logger
            )
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _util = util;
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LifeImageCloudConnection Connection { get; set; }

        public async Task wadoAsFileStream(
            LifeImageCloudConnection connection,
            int taskID,
            ImagingStudy study,
            IHttpManager httpManager,
            Series series = null,
            Instance instance = null,
            bool compress = true)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            string url = $"{study.url}";
            string dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar +
                         Constants.Dirs.ToRules + Path.DirectorySeparatorChar + Guid.NewGuid();
            long fileSize = 0;
            HttpResponseMessage response = null;
            MultipartFileStreamProvider streamProvider = null;
            MultipartFileStreamProvider contents = null;

            var httpClient = _liteHttpClient.GetClient(connection);

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} study url: {url} attempt {study.attempts}");

                if (series != null)
                {
                    Connection.URL += $"/series/{series.uid.Substring(8)}";

                    _logger.Log(LogLevel.Debug, $"{taskInfo} seriesURL: {url} attempt {series.attempts}");
                }

                if (instance != null)
                {
                    instance.downloadStarted = DateTime.Now;
                    instance.attempts++;
                    Connection.URL += $"/instances/{instance.uid}";

                    _logger.Log(LogLevel.Debug, $"{taskInfo} instanceURL: {url} attempt {instance.attempts}");
                }

                var cookies = _liteHttpClient.GetCookies(url);
                _logger.LogCookies(cookies, taskInfo);

                // issue the GET
                var task = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _taskManager.cts.Token);

                try
                {
                    response = await task;
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
                }

                // output the result                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                //if(Logger.logger.FileTraceLevel == "Verbose") _logger.Log(LogLevel.Debug,$"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        httpManager.loginNeeded = true;
                    }

                    _liteHttpClient.DumpHttpClientDetails();

                    return;
                }

                // 2018-05-09 shb need to get header from Cloud to tell us how big it is
                if (!_util.IsDiskAvailable(dir, _profileStorage.Current, 16000000000)) //just using 16GB as a catch all
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Insufficient disk to write {url} to {dir} guessing it could be 16GB");
                    return;
                    //throw new Exception($"Insufficient disk to write {url} to {dir} guessing it could be 16GB");
                }


                _logger.Log(LogLevel.Debug, $"{taskInfo} download dir will be {dir}");

                Directory.CreateDirectory(dir);
                streamProvider = new MultipartFileStreamProvider(dir);
                contents = await response.Content.ReadAsMultipartAsync(streamProvider, _taskManager.cts.Token);
                int index = 0;

                _logger.Log(LogLevel.Debug, $"{taskInfo} Splitting {contents.FileData.Count} files into RoutedItems.");
                foreach (var part in contents.FileData)
                {
                    try
                    {
                        index++;

                        fileSize += new System.IO.FileInfo(part.LocalFileName).Length;

                        _logger.Log(LogLevel.Debug, $"{taskInfo} downloaded file: {part.LocalFileName}");

                        RoutedItem routedItem = new RoutedItem(fromConnection: Connection.name, sourceFileName: part.LocalFileName,
                            taskID: taskID, fileIndex: index, fileCount: contents.FileData.Count)
                        {
                            type = RoutedItem.Type.DICOM,
                            Study = study.uid,
                            AccessionNumber = study.accession?.value,
                            //study.availability;
                            //routedItem.Description = study.description;
                            //study.extension;
                            //study.modalityList;
                            PatientID = study.patient?.display,
                            //study.referrer;
                            //study.resourceType;
                            Series = series.uid
                        };
                        //study.started;
                        //study.url;


                        _logger.Log(LogLevel.Debug, $"{taskInfo} Enqueuing RoutedItem {routedItem.sourceFileName}");

                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, taskInfo);
                    }
                }

                //2018-04-27 shb moved completion marking outside of part loop to avoid duplicate entries in markSeriesComplete
                //also added duplicate check.
                if (series != null)
                {
                    series.downloadCompleted = DateTime.Now;
                    lock (Connection.markSeriesComplete)
                    {
                        if (!Connection.markSeriesComplete.Contains(series))
                        {
                            Connection.markSeriesComplete.Add(series);
                        }
                    }
                }
                else if (instance != null)
                {
                    //means this came from studies calls so we need to mark this download as complete
                    instance.downloadCompleted = DateTime.Now;
                    lock (Connection.markDownloadsComplete)
                    {
                        Connection.markDownloadsComplete.Add(new string[] { url, "download-complete" });
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (NullReferenceException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} Exception: {e.Message} {e.StackTrace}");
                //throw e;
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
                _logger.LogFullException(e, taskInfo);
                _liteHttpClient.DumpHttpClientDetails();
            }
            finally
            {
                if (response != null) response.Dispose();

                _taskManager.Stop($"{Connection.name}.Wado");
            }

            stopWatch.Stop();
            _logger.Log(LogLevel.Information,
                $"{taskInfo} elapsed: {stopWatch.Elapsed} size: {fileSize} rate: {(float)fileSize / stopWatch.Elapsed.TotalMilliseconds * 1000 / 1000000} MB/s");
        }
    }
}
