using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILiteStoreService
    {
        Task store(List<RoutedItem> batch, int taskID, LITEConnection connection);
    }

    public sealed class LiteStoreService : ILiteStoreService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public LiteStoreService(
            ILiteHttpClient liteHttpClient,
            IRoutedItemManager routedItemManager,
            IProfileStorage profileStorage,
            ILITETask taskManager,
            ILogger<LiteStoreService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _routedItemManager = routedItemManager;
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        /// <summary>
        /// Store takes a batch of RoutedItem, all going to the same share destination, and uploads them as a single operation. This is done to solve the many small files problem.Larger files can go individually.
        /// </summary>
        /// <param name="batch"></param>
        /// <param name="taskID"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        public async Task store(List<RoutedItem> batch, int taskID, LITEConnection connection)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            StreamContent streamContent = null;
            MultipartContent content = null;
            HttpResponseMessage response = null;
            string testFile = null;

            var firstRecord = batch.First();

            try
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                //set the URL
                //string resourceURL = Connection.URL + "/api/File";
                string resourceURL = Connection.URL + FileAgentConstants.BaseUrl;
                _logger.Log(LogLevel.Debug, $"{taskInfo} URL: {resourceURL}");

                // generate guid for boundary...boundaries cannot be accidentally found in the content
                var boundary = Guid.NewGuid();
                _logger.Log(LogLevel.Debug, $"{taskInfo} boundary: {boundary}");

                // create the content
                content = new MultipartContent("related", boundary.ToString());

                //add the sharing headers
                List<string> shareHeader = new List<string>();
                if (Connection.shareDestinations != null)
                {
                    foreach (var connectionSet in firstRecord.toConnections.FindAll(e => e.connectionName.Equals(Connection.name)))
                    {
                        if (connectionSet.shareDestinations != null)
                        {
                            foreach (var shareDestination in connectionSet.shareDestinations)
                            {
                                shareHeader.Add(shareDestination.boxUuid);
                                _logger.Log(LogLevel.Debug, $"{taskInfo} sharing to: {shareDestination.boxId} {shareDestination.boxName} {shareDestination.groupId} {shareDestination.groupName} {shareDestination.organizationName} {shareDestination.publishableBoxType}");
                            }
                        }
                    }
                }

                content.Headers.Add("X-Li-Destination", shareHeader);

                long fileSize = 0;
                var profile = _profileStorage.Current;
                var dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toEGS";
                Directory.CreateDirectory(dir);
                testFile = dir + Path.DirectorySeparatorChar + Guid.NewGuid() + ".gz";

                using (FileStream compressedFileStream = File.Create(testFile))
                {
                    using GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
                    foreach (var routedItem in batch)
                    {
                        if (File.Exists(routedItem.sourceFileName))
                        {
                            routedItem.stream = File.OpenRead(routedItem.sourceFileName);

                            if (Connection.calcCompressionStats)
                            {
                                routedItem.stream.CopyTo(compressionStream);
                            }

                            fileSize += routedItem.length;
                            streamContent = new StreamContent(routedItem.stream);

                            streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                            {
                                FileName = routedItem.sourceFileName
                            };
                            content.Add(streamContent);

                            streamContent.Headers.Add("content-type", "application/octet-stream");
                        }
                        else
                        {
                            _logger.Log(LogLevel.Error, $"{taskInfo} {routedItem.sourceFileName} no longer exists.  Increase tempFileRetentionHours for heavy transfer backlogs that may take hours!!");
                            routedItem.Error = "File no longer exists";
                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toEGS, nameof(Connection.toEGS), error: true);
                        }
                    }
                }

                if (Connection.calcCompressionStats)
                {
                    FileInfo info = new FileInfo(testFile);
                    _logger.Log(LogLevel.Information, $"{taskInfo} orgSize: {fileSize} compressedSize: {info.Length} reduction: {(fileSize == 0 ? 0 : (fileSize * 1.0 - info.Length) / (fileSize) * 100)}%");

                }

                // issue the POST
                Task<HttpResponseMessage> task;

                var httpClient = _liteHttpClient.GetClient(connection);

                if (firstRecord.Compress == true)
                {
                    var compressedContent = new CompressedContent(content, "gzip");

                    _logger.Log(LogLevel.Debug, $"{taskInfo} compressedContent.Headers {compressedContent.Headers} ");

                    compressedContent.Headers.Remove("Content-Encoding");

                    var cookies = _liteHttpClient.GetCookies(resourceURL);
                    _logger.LogCookies(cookies, taskInfo);

                    task = httpClient.PostAsync(resourceURL, compressedContent);
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} will send content.Headers {content.Headers}");

                    var cookies = _liteHttpClient.GetCookies(resourceURL);
                    _logger.LogCookies(cookies, taskInfo);

                    task = httpClient.PostAsync(resourceURL, content);
                }

                response = await task;

                // output the result                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                _logger.Log(LogLevel.Debug, $"{taskInfo} response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                stopWatch.Stop();
                _logger.Log(LogLevel.Information, $"{taskInfo} elapsed: {stopWatch.Elapsed} size: {fileSize} rate: {(float)fileSize / stopWatch.Elapsed.TotalMilliseconds * 1000 / 1000000} MB/s");

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Created:
                        //dequeue the work, we're done!
                        if (streamContent != null) streamContent.Dispose();
                        if (response != null) response.Dispose();
                        if (content != null) content.Dispose();

                        foreach (var ri in batch)
                        {
                            _routedItemManager.Init(ri);
                            _routedItemManager.Dequeue(Connection, Connection.toEGS, nameof(Connection.toEGS));
                        }

                        //let EGGS know it's available, or when we convert udt to .net core then perhaps push so no open socket required on client.
                        //await SendToAllHubs(LITEServicePoint, batch);

                        break;
                    case HttpStatusCode.UnprocessableEntity:
                        //dequeue the work, we're done!
                        _logger.Log(LogLevel.Warning, $"creation of {firstRecord.sourceFileName} and others in batch failed with {response.StatusCode}");

                        if (streamContent != null) streamContent.Dispose();
                        if (response != null) response.Dispose();
                        if (content != null) content.Dispose();

                        foreach (var ri in batch)
                        {
                            ri.Error = HttpStatusCode.UnprocessableEntity.ToString();

                            _routedItemManager.Init(ri);
                            _routedItemManager.Dequeue(Connection, Connection.toEGS, nameof(Connection.toEGS), error: true);
                        }

                        break;
                    default:
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            Connection.loginNeeded = true;
                        }

                        _logger.Log(LogLevel.Warning, $"creation of {firstRecord.sourceFileName} and others in batch failed with {response.StatusCode}");

                        _liteHttpClient.DumpHttpClientDetails();
                        break;
                }

                //delete the compression test file
                File.Delete(testFile);

            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
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
            catch (FileNotFoundException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} {e.Message} {e.StackTrace}");

            }
            catch (IOException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} {e.Message} {e.StackTrace}");

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
                    if (streamContent != null) streamContent.Dispose();
                    if (response != null) response.Dispose();
                    if (content != null) content.Dispose();
                    File.Delete(testFile);
                    _taskManager.Stop($"{Connection.name}.Store");
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }
            }
        }
    }
}
