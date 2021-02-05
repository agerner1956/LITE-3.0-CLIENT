using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    /// <summary>
    /// Gets the cloud requests.
    /// </summary>
    public interface ICloudAgentTaskLoader
    {
        /// <summary>
        /// Gets the cloud requests.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="Connection"></param>
        /// <param name="cache"></param>
        /// <param name="httpManager"></param>
        /// <returns></returns>
        Task GetRequests(int taskID, LifeImageCloudConnection Connection, IConnectionRoutedCacheManager cache, IHttpManager httpManager);
    }

    public sealed class CloudAgentTaskLoader : ICloudAgentTaskLoader
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IRulesManager _rulesManager;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IPostResponseCloudService _postResponseCloudService;
        private readonly ISendToCloudService _sendToCloudService;
        private readonly ILITETask _taskManager;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILogger _logger;

        public CloudAgentTaskLoader(
            IProfileStorage profileStorage,
            IRulesManager rulesManager,
            IRoutedItemManager routedItemManager,
            IPostResponseCloudService postResponseCloudService,
            ISendToCloudService sendToCloudService,
            ILITETask taskManager,
            ILiteHttpClient liteHttpClient,
            ILogger<CloudAgentTaskLoader> logger)
        {
            _profileStorage = profileStorage;
            _rulesManager = rulesManager;
            _routedItemManager = routedItemManager;
            _postResponseCloudService = postResponseCloudService;
            _sendToCloudService = sendToCloudService;
            _taskManager = taskManager;
            _liteHttpClient = liteHttpClient;
            _logger = logger;
        }

        /// <summary>
        /// Gets the cloud requests.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="Connection"></param>
        /// <param name="cache"></param>
        /// <param name="httpManager"></param>
        /// <returns></returns>
        public async Task GetRequests(int taskID, LifeImageCloudConnection Connection, IConnectionRoutedCacheManager cache, IHttpManager httpManager)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            HttpResponseMessage response = null;

            var httpClient = _liteHttpClient.GetClient(Connection);

            try
            {
                while (!_taskManager.cts.IsCancellationRequested)
                {
                    await Task.Delay(_profileStorage.Current.KickOffInterval, _taskManager.cts.Token);

                    //BOUR-1022 shb I think the code that checks for dictionary entry before enqueuing is enough to prevent duplicate behavior
                    //skip if response cache is not empty
                    if (LifeImageCloudConnectionManager.cache.Count > 0)
                    {
                        int requestCount = 0;
                        //check to see if there are any requests
                        foreach (var cacheItem in LifeImageCloudConnectionManager.cache.ToArray())
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Cache entry id: {cacheItem.Key}");

                            foreach (var item in cacheItem.Value.ToArray())
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} fromConnection: {item.fromConnection} id: {item.id} started: {item.startTime} complete: {item.resultsTime} status: {item.status}");

                                if (item.type == RoutedItem.Type.RPC)
                                {
                                    requestCount++;
                                }
                            }
                        }

                        if (requestCount > 0)
                        {
                            //BOUR-1060 relax condition and rely on dictionary logic below
                            _logger.Log(LogLevel.Warning, $"{taskInfo} response cache has {requestCount} request items.");
                            // _logger.Log(LogLevel.Warning, $"{taskInfo} response cache has {requestCount} request items, skipping getting new requests until clear");
                            // return;
                        }
                    }

                    //set the URL
                    string agentTasksURL = Connection.URL + "/api/agent/v1/agent-tasks";

                    _logger.Log(LogLevel.Debug, $"{taskInfo} agentTasksURL: {agentTasksURL}");

                    var cookies = _liteHttpClient.GetCookies(agentTasksURL);
                    _logger.LogCookies(cookies, taskInfo);

                    // issue the GET
                    var task = httpClient.GetAsync(agentTasksURL);
                    response = await task;

                    // output the result                    
                    _logger.LogHttpResponseAndHeaders(response, taskInfo);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            httpManager.loginNeeded = true;
                        }

                        _logger.Log(LogLevel.Warning, $"{taskInfo} Problem getting agent tasks. {agentTasksURL} {response.StatusCode}");

                        _liteHttpClient.DumpHttpClientDetails();
                    }

                    // convert from stream to JSON
                    string results = await response.Content.ReadAsStringAsync();
                    var objResults = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(results);

                    foreach (var key in objResults)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} key: {key.Key}");
                        var list = key.Value;

                        foreach (var item in list)
                        {
                            foreach (var subkey in item)
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} subkey.Key: {subkey.Key} subKey.Value: {subkey.Value} ");
                            }
                        }
                    }

                    if (objResults.Count > 0)
                    {
                        // 2018-09-19 shb unwrap encoded task data and then send to rules
                        var agentRequestList = objResults["modelMapList"];
                        foreach (var agentRequest in agentRequestList)
                        {
                            byte[] data = Convert.FromBase64String(agentRequest["task"]);
                            string agentRequestAsString = Encoding.UTF8.GetString(data);
                            agentRequest["task"] = agentRequestAsString;
                            string id = agentRequest["id"];
                            string request = agentRequest["task"];
                            string requestType = agentRequest["task_type"];
                            string connection = null;
                            agentRequest.TryGetValue("connection", out connection);
                            RoutedItem ri = new RoutedItem(fromConnection: Connection.name, id: id, request: request,
                                requestType: requestType)
                            {
                                type = RoutedItem.Type.RPC,
                                status = RoutedItem.Status.PENDING,
                                startTime = DateTime.Now,
                                TaskID = taskID
                            };
                            if (connection != null && connection != "*")
                            {
                                ConnectionSet connSet = new ConnectionSet
                                {
                                    connectionName = connection
                                };
                                ri.toConnections.Add(connSet);
                            }

                            LifeImageCloudConnectionManager.cache.TryGetValue(ri.id, out List<RoutedItem> cacheItem);
                            if (cacheItem == null)
                            {
                                //determine which connections will need to reply and prime the response cache
                                _rulesManager.Init(_profileStorage.Current.rules);
                                //var connsets = _profileStorage.Current.rules.Eval(ri);
                                var connsets = _rulesManager.Eval(ri);
                                foreach (var connset in connsets)
                                {
                                    _routedItemManager.Init(ri);
                                    var prime = (RoutedItem)_routedItemManager.Clone();
                                    prime.startTime = DateTime.Now; //clock starts ticking now
                                    prime.status = RoutedItem.Status.PENDING;
                                    prime.fromConnection = _profileStorage.Current.connections.Find(e => e.name == connset.connectionName).name;
                                    _logger.Log(LogLevel.Debug,
                                        $"{taskInfo} Priming Response cache id: {id} conn: {prime.fromConnection} ");
                                    cache.Route(prime);
                                }

                                //enqueue the request

                                _logger.Log(LogLevel.Debug, $"{taskInfo} Enqueuing id: {id} requestType: {requestType} subKey.Value: {request} ");

                                _routedItemManager.Init(ri);
                                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                                //BOUR-995 let cloud know we got the request with a status of PENDING
                                await _postResponseCloudService.PostResponse(Connection, ri, cache, httpManager, taskID);
                            }
                            else
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} Exists id: {id} requestType: {requestType} subKey.Value: {request} ");
                                foreach (var item in cacheItem)
                                {
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} fromConnection: {item.fromConnection} started: {item.startTime} complete: {item.resultsTime} status: {item.status}");
                                    foreach (var ctr in item.cloudTaskResults)
                                    {
                                        foreach (var result in ctr.results)
                                        {
                                            _logger.Log(LogLevel.Debug, $"{taskInfo} Exists id: {item.id} results: {result}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (System.InvalidOperationException) //for the Collection was Modified, we can wait
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Waiting for requests to complete before getting new requests");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                try
                {
                    _taskManager.Stop($"{Connection.name}.GetRequests");
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
