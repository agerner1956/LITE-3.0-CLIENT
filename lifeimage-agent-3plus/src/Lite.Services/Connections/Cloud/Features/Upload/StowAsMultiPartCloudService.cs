using Lite.Core.Connections;
using Lite.Core.Guard;
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

namespace Lite.Services.Connections.Cloud.Features
{
    public interface IStowAsMultiPartCloudService
    {
        /// <summary>
        /// stowAsMultiPart takes a batch of RoutedItem, all going to the same share destination, and uploads them as a single operation.
        ///    This is done to solve the many small files problem.Larger files can go individually.
        /// </summary>
        /// <param name="batch"></param>
        /// <param name="taskID"></param>
        /// <param name="Connection"></param>
        /// <param name="httpManager"></param>
        /// <returns></returns>
        Task stowAsMultiPart(List<RoutedItem> batch, int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager);
    }

    public sealed class StowAsMultiPartCloudService : IStowAsMultiPartCloudService
    {
        private readonly ILogger _logger;
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;

        public StowAsMultiPartCloudService(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<StowAsMultiPartCloudService> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task stowAsMultiPart(List<RoutedItem> batch, int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager)
        {
            Throw.IfNull(Connection);
            Throw.IfNull(httpManager);

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            StreamContent streamContent = null;
            MultipartContent content = null;
            HttpResponseMessage response = null;
            string testFile = null;

            var firstRecord = batch.First();

            var httpClient = _liteHttpClient.GetClient(Connection);

            try
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();


                //set the URL
                string stowURL = Connection.URL + "/api/agent/v1/stow/studies";
                _logger.Log(LogLevel.Debug, $"{taskInfo} stowURL: {stowURL}");

                // generate guid for boundary...boundaries cannot be accidentally found in the content
                var boundary = Guid.NewGuid();
                _logger.Log(LogLevel.Debug, $"{taskInfo} boundary: {boundary}");

                // create the content
                content = new MultipartContent("related", boundary.ToString());

                //add the sharing headers
                List<string> shareHeader = new List<string>();
                if (Connection.shareDestinations != null)
                {
                    foreach (var connectionSet in firstRecord.toConnections.FindAll(e =>
                        e.connectionName.Equals(Connection.name)))
                    {
                        if (connectionSet.shareDestinations != null)
                        {
                            foreach (var shareDestination in connectionSet.shareDestinations)
                            {
                                shareHeader.Add(shareDestination.boxUuid);

                                _logger.Log(LogLevel.Debug,
                                    $"{taskInfo} sharing to: {shareDestination.boxId} {shareDestination.boxName} {shareDestination.groupId} {shareDestination.groupName} {shareDestination.organizationName} {shareDestination.publishableBoxType}");
                            }
                        }
                    }
                }

                content.Headers.Add("X-Li-Destination", shareHeader);

                long fileSize = 0;
                var dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toCloud";
                Directory.CreateDirectory(dir);
                testFile = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid() + ".gz";

                using (FileStream compressedFileStream = File.Create(testFile))
                {
                    using GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
                    foreach (var routedItem in batch)
                    {
                        if (File.Exists(routedItem.sourceFileName))
                        {
                            routedItem.stream = File.OpenRead(routedItem.sourceFileName);

                            if (Connection.CalcCompressionStats)
                            {
                                routedItem.stream.CopyTo(compressionStream);
                            }

                            fileSize += routedItem.length;
                            streamContent = new StreamContent(routedItem.stream);

                            streamContent.Headers.ContentDisposition =
                                new ContentDispositionHeaderValue("attachment")
                                {
                                    FileName = routedItem.sourceFileName
                                };
                            content.Add(streamContent);

                            //shb 2017-11-01 streamcontent (uncompressed) is inside content (can be compressed below) server is complaining so will comment out
                            //streamContent.Headers.Add("Content-Transfer-Encoding", "gzip");

                            //shb 2017-11-10 shb added content-type header to solve
                            //controller.DicomRSControllerBase: Content-Encoding header has value null !!!
                            //controller.StowRSController: Unable to process part 1 no content-type parameter received
                            streamContent.Headers.Add("content-type", "application/dicom");
                        }
                        else
                        {
                            _logger.Log(LogLevel.Error,
                                $"{taskInfo} {routedItem.sourceFileName} no longer exists.  Increase tempFileRetentionHours for heavy transfer backlogs that may take hours!!");
                        }
                    }
                }

                if (Connection.CalcCompressionStats)
                {
                    FileInfo info = new FileInfo(testFile);
                    _logger.Log(LogLevel.Information, $"{taskInfo} orgSize: {fileSize} compressedSize: {info.Length} reduction: {(fileSize == 0 ? 0 : (fileSize * 1.0 - info.Length) / (fileSize) * 100)}%");
                }

                // issue the POST
                Task<HttpResponseMessage> task;

                if (firstRecord.Compress == true)
                {
                    var compressedContent = new CompressedContent(content, "gzip");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} compressedContent.Headers {compressedContent.Headers} ");

                    var cookies = _liteHttpClient.GetCookies(stowURL);
                    _logger.LogCookies(cookies, taskInfo);

                    task = httpClient.PostAsync(stowURL, compressedContent, _taskManager.cts.Token);
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} will send content.Headers {content.Headers}");

                    var cookies = _liteHttpClient.GetCookies(stowURL);
                    _logger.LogCookies(cookies, taskInfo);

                    task = httpClient.PostAsync(stowURL, content, _taskManager.cts.Token);
                }

                response = await task;

                // output the result                                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                _logger.Log(LogLevel.Debug, $"{taskInfo} response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                stopWatch.Stop();

                _logger.Log(LogLevel.Information, $"{taskInfo} elapsed: {stopWatch.Elapsed} size: {fileSize} rate: {(float)fileSize / stopWatch.Elapsed.TotalMilliseconds * 1000 / 1000000} MB/s");

                if (!(response.StatusCode == HttpStatusCode.OK ||
                      response.StatusCode == HttpStatusCode.Accepted))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        httpManager.loginNeeded = true;
                    }

                    _logger.Log(LogLevel.Warning,
                        $"stow of {firstRecord.sourceFileName} and others in batch failed with {response.StatusCode}");

                    _liteHttpClient.DumpHttpClientDetails();
                }
                else
                {
                    //dequeue the work, we're done!
                    if (streamContent != null) streamContent.Dispose();
                    if (response != null) response.Dispose();
                    if (content != null) content.Dispose();

                    foreach (var ri in batch)
                    {
                        _routedItemManager.Init(ri);
                        _routedItemManager.Dequeue(Connection, Connection.toCloud, nameof(Connection.toCloud));
                    }
                }

                //delete the compression test file
                File.Delete(testFile);
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
                    _taskManager.Stop($"{Connection.name}.Stow");
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, taskInfo);
                }
            }
        }
    }
}
