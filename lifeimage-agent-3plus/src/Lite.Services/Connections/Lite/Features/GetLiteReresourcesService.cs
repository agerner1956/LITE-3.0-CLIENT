using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface IGetLiteReresourcesService
    {
        Task<int> GetResources(int taskID, LITEConnection connection, IHttpManager manager);
    }

    public sealed class GetLiteReresourcesService : IGetLiteReresourcesService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly IRoutedItemManager _routedItemManager;         
        private readonly ILogger _logger;

        public GetLiteReresourcesService(
            ILiteHttpClient liteHttpClient,
            IRoutedItemManager routedItemManager,            
            ILogger<GetLiteReresourcesService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _routedItemManager = routedItemManager;            
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public async Task<int> GetResources(int taskID, LITEConnection connection, IHttpManager manager)
        {
            Connection = connection;

            //get the resource list from EGS
            try
            {

                var httpClient = _liteHttpClient.GetClient(connection);

                var taskInfo = $"task: {taskID} connection: {Connection.name}";
                foreach (var shareDestination in Connection.boxes)
                {
                    //set the URL
                    string url = Connection.URL + "/api/File/" + shareDestination.boxUuid;  //add summary 
                    _logger.Log(LogLevel.Debug, $"{taskInfo} URL: {url}");

                    var cookies = _liteHttpClient.GetCookies(url);
                    _logger.LogCookies(cookies, taskInfo);

                    // issue the GET
                    var task = httpClient.GetAsync(url);
                    var response = await task;

                    // output the result                    
                    _logger.LogHttpResponseAndHeaders(response, taskInfo);

                    _logger.Log(LogLevel.Debug, $"{taskInfo} response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            manager.loginNeeded = true;
                        }

                        _logger.Log(LogLevel.Warning, $"{taskInfo} {response.StatusCode} {response.ReasonPhrase}");

                        _liteHttpClient.DumpHttpClientDetails();
                    }

                    //2018-02-06 shb convert from stream to JSON and clean up any non UTF-8 that appears like it did
                    // when receiving "contains invalid UTF8 bytes" exception
                    // var serializer = new DataContractJsonSerializer(typeof(Files));
                    // var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.UTF8);
                    // byte[] byteArray = Encoding.UTF8.GetBytes(streamReader.ReadToEnd());
                    // MemoryStream stream = new MemoryStream(byteArray);

                    // var newResources = serializer.ReadObject(stream) as Files;  //List<EGSFileInfo>

                    var newResources = JsonSerializer.Deserialize<FilesModel>(await response.Content.ReadAsStringAsync());

                    if (newResources != null && newResources.files.Count > 0)
                    {
                        lock (Connection.fromEGS)
                        {
                            //take the new studies from cloud and merge with existing
                            foreach (var ri in newResources.files)
                            {
                                if (!Connection.fromEGS.Any(e => e.resource == ri.resource))
                                {
                                    _logger.Log(LogLevel.Information, $"Adding {ri.resource}");

                                    _routedItemManager.Init(ri);

                                    _routedItemManager.Enqueue(connection, connection.fromEGS, nameof(connection.fromEGS), copy: false);
                                }
                                else
                                {
                                    _logger.Log(LogLevel.Error, $"Resource already exists: {ri.resource}");
                                }
                            }
                        }
                        return newResources.files.Count;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                //eat it for now
                _logger.Log(LogLevel.Warning, $"{e.Message} {e.StackTrace}");
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                _logger.Log(LogLevel.Warning, $"{e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
                //throw e;                
            }

            return 0;
        }
    }
}
