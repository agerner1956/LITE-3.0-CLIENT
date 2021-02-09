using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface IDownloadViaHttpService
    {
        Task DownloadViaHttp(int taskID, RoutedItem routedItem, LITEConnection connection, IHttpManager httpManager, bool compress = true);
    }

    public sealed class DownloadViaHttpService : IDownloadViaHttpService
    {
        private readonly IDeleteEGSResourceService _deleteEGSResourceService;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly IDiskUtils _util;
        private readonly ILogger _logger;

        public DownloadViaHttpService(
            IDeleteEGSResourceService deleteEGSResourceService,
            ILiteHttpClient liteHttpClient,
            IRoutedItemManager routedItemManager,
            IProfileStorage profileStorage,
            IDiskUtils util,
            ILITETask taskManager,
            ILogger<DownloadViaHttpService> logger)
        {
            _deleteEGSResourceService = deleteEGSResourceService;
            _liteHttpClient = liteHttpClient;
            _routedItemManager = routedItemManager;
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _util = util;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        /// <summary>
        /// wado downloads studies from liCloud.  ImagingStudy is required while Series and Instance are optional.  RAM utilization remains low regardless of download size.  
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="routedItem"></param>
        /// <param name="connection"></param>
        /// <param name="httpManager"></param>
        /// <param name="compress"></param>
        /// <returns></returns>
        public async Task DownloadViaHttp(int taskID, RoutedItem routedItem, LITEConnection connection, IHttpManager httpManager, bool compress = true)
        {
            Connection = connection;
            var taskInfo = $"task: {taskID} connection: {Connection.name} resource: {routedItem.resource}";
            var profile = _profileStorage.Current;
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            //string url = Connection.URL + $"/api/File/{routedItem.box}/{routedItem.resource}";
            string url = Connection.URL + FileAgentConstants.GetDownloadUrl(routedItem);
            string dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + Constants.Dirs.ToRules + Path.DirectorySeparatorChar + Guid.NewGuid();
            Directory.CreateDirectory(dir);
            long fileSize = 0;
            HttpResponseMessage response = null;
            MultipartFileStreamProvider streamProvider = null;
            MultipartFileStreamProvider contents = null;

            var httpClient = _liteHttpClient.GetClient(connection);

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} download dir will be {dir}");
                _logger.Log(LogLevel.Debug, $"{taskInfo} url: {url} attempt: {routedItem.attempts}");

                var cookies = _liteHttpClient.GetCookies(url);
                _logger.LogCookies(cookies, taskInfo);

                // issue the GET
                var task = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _taskManager.cts.Token);

                try
                {
                    response = await task.ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
                }

                // output the result                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                //if(Logger.logger.FileTraceLevel == "Verbose") _logger.Log(LogLevel.Debug,$"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        break;
                    case HttpStatusCode.NotFound:
                        routedItem.Error = HttpStatusCode.NotFound.ToString();

                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.fromEGS, nameof(Connection.fromEGS), error: true);
                        return;
                    case HttpStatusCode.Unauthorized:
                        httpManager.loginNeeded = true;
                        _liteHttpClient.DumpHttpClientDetails();
                        return;
                    default:
                        _liteHttpClient.DumpHttpClientDetails();
                        return;
                }

                if (!_util.IsDiskAvailable(dir, profile, routedItem.length))
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Insufficient disk to write {url} to {dir} guessing it could be 16GB");
                    return;
                }

                streamProvider = new MultipartFileStreamProvider(dir, 1024000);

                try
                {
                    contents = await response.Content.ReadAsMultipartAsync(streamProvider, _taskManager.cts.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    //MIME is corrupt such as Unexpected end of MIME multipart stream. MIME multipart message is not complete.
                    //This usually happens if the upload does not complete.  Catch as "normal" and remove resource as if success
                    //since retrying will not help this condition.

                    _logger.LogFullException(e, taskInfo);

                    _liteHttpClient.DumpHttpClientDetails();

                    if (await _deleteEGSResourceService.DeleteEGSResource(taskID, routedItem, connection, httpManager).ConfigureAwait(false))
                    {
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.fromEGS, nameof(Connection.fromEGS), error: false);
                    }
                    else
                    {
                        routedItem.Error = "Unable to delete EGS resource";
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.fromEGS, nameof(Connection.fromEGS), error: true);
                    }

                    return;
                }

                int index = 0;
                _logger.Log(LogLevel.Debug, $"{taskInfo} Splitting {contents?.FileData.Count} files into RoutedItems.");
                foreach (var part in contents.FileData)
                {
                    try
                    {
                        index++;

                        fileSize += new FileInfo(part.LocalFileName).Length;

                        _logger.Log(LogLevel.Debug, $"{taskInfo} downloaded file: {part.LocalFileName}");

                        RoutedItem ri = new RoutedItem(fromConnection: Connection.name, sourceFileName: part.LocalFileName, taskID: taskID, fileIndex: index, fileCount: contents.FileData.Count)
                        {
                            type = RoutedItem.Type.FILE
                        };

                        _logger.Log(LogLevel.Debug, $"{taskInfo} Enqueuing RoutedItem {routedItem.sourceFileName}");

                        _routedItemManager.Init(ri);
                        _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, taskInfo);
                    }
                }

                if (await _deleteEGSResourceService.DeleteEGSResource(taskID, routedItem, connection, httpManager).ConfigureAwait(false))
                {
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(Connection, Connection.fromEGS, nameof(Connection.fromEGS), error: false);
                }
                else
                {
                    routedItem.Error = "Unable to delete EGS resource";
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(Connection, Connection.fromEGS, nameof(Connection.fromEGS), error: true);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
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
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
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

                _taskManager.Stop($"{Connection.name}.DownloadViaHttp");
            }

            stopWatch.Stop();
            _logger.Log(LogLevel.Information, $"{taskInfo} elapsed: {stopWatch.Elapsed} size: {fileSize} rate: {(float)fileSize / stopWatch.Elapsed.TotalMilliseconds * 1000 / 1000000} MB/s");
        }
    }
}
