using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ICloudShareDestinationsService
    {
        Task GetShareDestinations(int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager);
    }

    public sealed class CloudShareDestinationsService : ICloudShareDestinationsService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public CloudShareDestinationsService(
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<CloudShareDestinationsService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task GetShareDestinations(int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager)
        {
            Throw.IfNull(Connection);
            Throw.IfNull(httpManager);

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Processing getShareDestinations");

                var httpClient = _liteHttpClient.GetClient(Connection);

                try
                {
                    //set the URL
                    string shareURL = Connection.URL + "/api/box/v3/listAllPublishable";

                    _logger.Log(LogLevel.Debug, $"{taskInfo} shareURL: {shareURL}");

                    var cookies = _liteHttpClient.GetCookies(shareURL);
                    _logger.LogCookies(cookies, taskInfo);

                    // issue the GET
                    var task = httpClient.GetAsync(shareURL);
                    var response = await task;

                    // output the result                    
                    _logger.LogHttpResponseAndHeaders(response, taskInfo);
                    _logger.Log(LogLevel.Debug, $"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            httpManager.loginNeeded = true;
                        }

                        _logger.Log(LogLevel.Warning, $"{taskInfo} Problem getting share destinations. {response.StatusCode}");

                        _liteHttpClient.DumpHttpClientDetails();
                    }

                    // convert from stream to JSON
                    var serializer = new DataContractJsonSerializer(typeof(List<ShareDestinations>));
                    Connection.shareDestinations =
                        serializer.ReadObject(await response.Content.ReadAsStreamAsync()) as List<ShareDestinations>;
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
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.getShareDestinations");
            }
        }
    }
}
