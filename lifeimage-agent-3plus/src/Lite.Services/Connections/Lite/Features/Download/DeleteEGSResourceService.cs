using Lite.Core.Connections;
using Lite.Core.Models;
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
    public interface IDeleteEGSResourceService
    {
        Task<bool> DeleteEGSResource(int taskID, RoutedItem routedItem, LITEConnection connection, IHttpManager httpManager);
    }

    public sealed class DeleteEGSResourceService : IDeleteEGSResourceService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public DeleteEGSResourceService(
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<DeleteEGSResourceService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }
        public async Task<bool> DeleteEGSResource(int taskID, RoutedItem routedItem, LITEConnection connection, IHttpManager httpManager)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            HttpResponseMessage response = null;

            //string resourceURL = Connection.URL + "/api/File/" + routedItem.box + "/" + routedItem.resource;
            string resourceURL = Connection.URL + FileAgentConstants.GetDeleteUrl(routedItem);

            var httpClient = _liteHttpClient.GetClient(Connection);

            try
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                //set the URL
                _logger.Log(LogLevel.Debug, $"{taskInfo} URL: {resourceURL}");


                // issue the POST
                Task<HttpResponseMessage> task;

                task = httpClient.DeleteAsync(resourceURL, _taskManager.cts.Token);

                response = await task.ConfigureAwait(false);

                // output the result                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                stopWatch.Stop();
                _logger.Log(LogLevel.Information, $"{taskInfo} elapsed: {stopWatch.Elapsed}");

                switch (response.StatusCode)
                {
                    case HttpStatusCode.NoContent:
                        return true;

                    default:
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            httpManager.loginNeeded = true;
                        }

                        _logger.Log(LogLevel.Warning, $"Deletion of {resourceURL} failed with {response.StatusCode}");

                        _liteHttpClient.DumpHttpClientDetails();

                        return false;
                }
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
                if (response != null) response.Dispose();
            }

            return false;
        }
    }
}
