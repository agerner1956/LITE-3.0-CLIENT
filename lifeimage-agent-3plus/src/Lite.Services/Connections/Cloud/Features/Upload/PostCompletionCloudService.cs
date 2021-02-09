using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface IPostCompletionCloudService
    {
        /// <summary>
        /// Sends the results back to cloud.
        /// </summary>
        /// <param name="Connection"></param>
        /// <param name="routedItem"></param>
        /// <param name="taskID"></param>
        /// <param name="cacheManager">IConnectionRoutedCacheManager</param>
        /// <param name="httpManager"></param>
        /// <returns></returns>
        Task PostCompletion(LifeImageCloudConnection Connection, RoutedItem routedItem, IConnectionRoutedCacheManager cacheManager, IHttpManager httpManager, long taskID);
    }

    public sealed class PostCompletionCloudService : IPostCompletionCloudService
    {
        private readonly ILogger _logger;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILITETask _taskManager;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ICloudConnectionCacheAccessor _cloudConnectionCacheAccessor;

        public PostCompletionCloudService(
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILiteHttpClient liteHttpClient,
            ICloudConnectionCacheAccessor cloudConnectionCacheAccessor,
            ILogger<PostCompletionCloudService> logger)
        {
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _liteHttpClient = liteHttpClient;
            _cloudConnectionCacheAccessor = cloudConnectionCacheAccessor;
            _logger = logger;
        }

        public async Task PostCompletion(LifeImageCloudConnection Connection, RoutedItem routedItem, IConnectionRoutedCacheManager cacheManager, IHttpManager httpManager, long taskID)
        {
            Throw.IfNull(Connection);
            Throw.IfNull(routedItem);
            Throw.IfNull(cacheManager);
            Throw.IfNull(httpManager);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var taskInfo = $"task: {taskID} connection: {Connection.name} id: {routedItem.id} ";

            HttpResponseMessage response = null;


            var httpClient = _liteHttpClient.GetClient(Connection);

            try
            {
                if (routedItem.Study == null || routedItem.Study == "")
                {
                    _logger.Log(LogLevel.Warning,
                        $"{taskInfo} meta: {routedItem.RoutedItemMetaFile} cannot close routedItem.Study: {routedItem.Study} because null or blank.");
                    cacheManager.RemoveCachedItem(routedItem);
                    return;
                }

                //POST /api/agent/v1/study/{studyInstanceUid}/upload-close
                //string studyCloseURL = Connection.URL + $"/api/agent/v1/study/{routedItem.Study}/upload-close";
                string studyCloseURL = Connection.URL + CloudAgentConstants.GetUploadCloseUrl(routedItem.Study);

                _logger.Log(LogLevel.Debug, $"{taskInfo} studyCloseURL: {studyCloseURL}");

                var metadata = "";

                try
                {
                    metadata = _cloudConnectionCacheAccessor.GetCachedItemMetaData(Connection, routedItem, taskID);
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} Unable to produce metadata for {routedItem.id} {routedItem.RoutedItemMetaFile}: {e.Message} {e.StackTrace}");
                }

                using (HttpContent httpContent = new StringContent(metadata))
                {
                    var cookies = _liteHttpClient.GetCookies(studyCloseURL);
                    _logger.LogCookies(cookies, taskInfo);

                    response = await httpClient.PostAsync(studyCloseURL, httpContent, _taskManager.cts.Token);

                    // output the result                    
                    _logger.LogHttpResponseAndHeaders(response, taskInfo);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        httpManager.loginNeeded = true;
                    }

                    //BOUR-995 we don't want to dequeue unless completed or failed
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud), error: false);
                        cacheManager.RemoveCachedItem(routedItem);
                    }

                    //BOUR-995 we don't want to dequeue unless completed or failed
                    if ((response.StatusCode == HttpStatusCode.InternalServerError) ||
                         response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} {response.StatusCode} {response.ReasonPhrase}. Dequeuing to error folder");
                        _liteHttpClient.DumpHttpClientDetails();
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud), error: true);
                        cacheManager.RemoveCachedItem(routedItem);
                    }
                }

                stopWatch.Stop();
                _logger.Log(LogLevel.Information, $"{taskInfo} elapsed: {stopWatch.Elapsed}");
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
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
                cacheManager.RemoveCachedItem(routedItem);
            }
            finally
            {
                try
                {
                    _taskManager.Stop($"{Connection.name}.PostCompletion");
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
