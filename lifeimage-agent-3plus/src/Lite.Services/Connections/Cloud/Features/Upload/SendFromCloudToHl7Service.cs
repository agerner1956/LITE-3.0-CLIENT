using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ISendFromCloudToHl7Service
    {
        Task putHL7(RoutedItem routedItem, int taskID, LifeImageCloudConnection connection, IHttpManager httpManager);
    }

    public sealed class SendFromCloudToHl7Service : ISendFromCloudToHl7Service
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public SendFromCloudToHl7Service(
            ILiteHttpClient liteHttpClient,
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILogger<SendFromCloudToHl7Service> logger)
        {
            _liteHttpClient = liteHttpClient;
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task putHL7(RoutedItem routedItem, int taskID, LifeImageCloudConnection connection, IHttpManager httpManager)
        {
            var Connection = connection;

            var httpClient = _liteHttpClient.GetClient(connection);

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            MultipartContent content = null;
            StreamContent streamContent = null;
            HttpResponseMessage response = null;

            try
            {
                if (!File.Exists(routedItem.sourceFileName))
                {
                    routedItem.Error = "File Not Found";
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud), error: true);
                    return;
                }

                var stopWatch = new Stopwatch();
                stopWatch.Start();

                //set theConnection.URL http://localhost:8080/universal-inbox/api/agent/v1/hl7-upload
                //string putHL7URL = Connection.URL + "/api/agent/v1/hl7-upload?connectionName=" + routedItem.fromConnection;
                string putHL7URL = Connection.URL + CloudAgentConstants.GetPutHl7Url(routedItem.fromConnection);
                _logger.Log(LogLevel.Debug, $"{taskInfo} putHL7URL: {putHL7URL}");

                //generate guid for boundary...boundaries cannot be accidentally found in the content
                var boundary = Guid.NewGuid();
                _logger.Log(LogLevel.Debug, $"{taskInfo} boundary: {boundary}");

                // create the content
                content = new MultipartContent("related", boundary.ToString());

                //add the sharing headers
                List<string> shareHeader = new List<string>();
                if (Connection.shareDestinations != null)
                {
                    foreach (var connectionSet in routedItem.toConnections.FindAll(e =>
                        e.connectionName.Equals(Connection.name)))
                    {
                        if (connectionSet.shareDestinations != null)
                        {
                            foreach (var shareDestination in connectionSet.shareDestinations)
                            {
                                shareHeader.Add(shareDestination.boxUuid);
                            }
                        }
                    }
                }

                content.Headers.Add("X-Li-Destination", shareHeader);

                //
                //var fileSize = routedItem.stream.Length;

                var fileSize = new FileInfo(routedItem.sourceFileName).Length;
                //var streamContent = new StreamContent(routedItem.stream);
                streamContent = new StreamContent(File.OpenRead(routedItem.sourceFileName));

                streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    //               FileName = filename
                    FileName = routedItem.sourceFileName
                };
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                //streamContent.Headers.Add("Content-Transfer-Encoding", "gzip");

                content.Add(streamContent);

                // issue the POST
                Task<HttpResponseMessage> task;

                var cookies = _liteHttpClient.GetCookies(putHL7URL);
                _logger.LogCookies(cookies, taskInfo);

                if (routedItem.Compress == true)
                {
                    task = httpClient.PostAsync(putHL7URL, new CompressedContent(content, "gzip"), _taskManager.cts.Token);
                }
                else
                {
                    task = httpClient.PostAsync(putHL7URL, content, _taskManager.cts.Token);
                }

                response = await task;

                // output the result                               
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    httpManager.loginNeeded = true;
                }

                _logger.Log(LogLevel.Debug,
                    $"{taskInfo} response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                // convert from stream to JSON
                //var serializer = new DataContractJsonSerializer(typeof(LoginJSON));
                //var loginJSON = serializer.ReadObject(await response.Content.ReadAsStreamAsync()) as LoginJSON;

                stopWatch.Stop();
                _logger.Log(LogLevel.Information,
                    $"{taskInfo} elapsed: {stopWatch.Elapsed} size: {fileSize} rate: {(float)fileSize / stopWatch.Elapsed.TotalMilliseconds * 1000 / 1000000} MB/s");

                //dequeue the work, we're done!
                if (streamContent != null) streamContent.Dispose();
                if (response != null) response.Dispose();
                if (content != null) content.Dispose();

                _routedItemManager.Init(routedItem);
                _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud));
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
                    _taskManager.Stop($"{Connection.name}.putHL7");
                    if (streamContent != null) streamContent.Dispose();
                    if (response != null) response.Dispose();
                    if (content != null) content.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }
            }
        }
    }
}
