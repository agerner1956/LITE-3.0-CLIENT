using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface IPostResponseCloudService
    {
        /// <summary>
        ///  Sends the results back to cloud
        /// </summary>
        /// <param name="Connection"></param>
        /// <param name="routedItem"></param>
        /// <param name="cacheManager"></param>
        /// <param name="httpManager"></param>
        /// <param name="taskID"></param>
        /// <returns></returns>
        Task PostResponse(LifeImageCloudConnection Connection, RoutedItem routedItem, IConnectionRoutedCacheManager cacheManager, IHttpManager httpManager, long taskID);
    }

    public sealed class PostResponseCloudService : IPostResponseCloudService
    {
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public PostResponseCloudService(
            IRoutedItemManager routedItemManager,
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<PostResponseCloudService> logger)
        {
            _routedItemManager = routedItemManager;
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task PostResponse(LifeImageCloudConnection Connection, RoutedItem routedItem, IConnectionRoutedCacheManager cacheManager, IHttpManager httpManager, long taskID)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var taskInfo = $"task: {taskID} connection: {Connection.name} id: {routedItem.id} ";

            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} request: {routedItem.request}");
                foreach (var results in routedItem.response)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} response: {results}");
                }
            }

            HttpResponseMessage response = null;

            try
            {
                string json = JsonSerializer.Serialize(routedItem.cloudTaskResults);

                _logger.Log(LogLevel.Debug, $"{taskInfo} posting {json}");
                string base64Results = Convert.ToBase64String(Encoding.ASCII.GetBytes(json));
                string agentTasksURL = Connection.URL + $"/api/agent/v1/agent-task-results/{routedItem.id}";

                //optional status="NEW", "PENDING", "COMPLETED", "FAILED"
                agentTasksURL += $"?status={routedItem.status}";


                _logger.Log(LogLevel.Debug, $"{taskInfo} agentTasksURL: {agentTasksURL}");

                var httpClient = _liteHttpClient.GetClient(Connection);

                using (HttpContent httpContent = new StringContent(base64Results))
                {
                    var cookies = _liteHttpClient.GetCookies(agentTasksURL);
                    _logger.LogCookies(cookies, taskInfo);

                    response = await httpClient.PostAsync(agentTasksURL, httpContent, _taskManager.cts.Token);

                    // output the result                    
                    _logger.LogHttpResponseAndHeaders(response, taskInfo);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        httpManager.loginNeeded = true;
                    }

                    //BOUR-995 we don't want to dequeue unless completed or failed
                    if (response.StatusCode == HttpStatusCode.OK &&
                        (routedItem.status == RoutedItem.Status.COMPLETED ||
                         routedItem.status == RoutedItem.Status.FAILED))
                    {
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud), error: false);
                        cacheManager.RemoveCachedItem(routedItem);
                    }

                    //BOUR-995 we don't want to dequeue unless completed or failed
                    if ((response.StatusCode == HttpStatusCode.InternalServerError ||
                         response.StatusCode == HttpStatusCode.BadRequest) &&
                        (routedItem.status == RoutedItem.Status.COMPLETED ||
                         routedItem.status == RoutedItem.Status.FAILED))
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
            }
            finally
            {
                try
                {
                    _taskManager.Stop($"{Connection.name}.PostResponse");
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
