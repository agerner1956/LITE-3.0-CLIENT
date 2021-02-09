using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud
{
    public interface ICloudRegisterService
    {
        Task register(int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager);
        Task<RegisterAsAdmin> register(LifeImageCloudConnection Connection, string username, string password, string org = null, string serviceName = null);
    }

    public sealed class CloudRegisterService : ICloudRegisterService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public CloudRegisterService(
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ILogger<CloudRegisterService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _logger = logger;
        }

        // register to get a tenantID, can only be done one time
        public async Task register(int taskID, LifeImageCloudConnection Connection, IHttpManager httpManager)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var httpClient = _liteHttpClient.GetClient(Connection);

            try
            {
                //set the URL
                string registerURL = Connection.URL + CloudAgentConstants.RegisterAsOrgUrl;

                _logger.Log(LogLevel.Debug, $"{taskInfo} registerURL: {registerURL}");

                //set the form parameters
                var registerParams = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("organizationCode", Connection.organizationCode),
                    new KeyValuePair<string, string>("subscriptionCode", Connection.subscriptionCode)
                });

                // issue the POST
                var task = httpClient.PostAsync(registerURL, registerParams, _taskManager.cts.Token);
                var response = await task;

                // output the result                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    httpManager.loginNeeded = true;
                }
                
                _logger.Log(LogLevel.Debug, $"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                // convert from stream to JSON
                var serializer = new DataContractJsonSerializer(typeof(Register));
                var register = serializer.ReadObject(await response.Content.ReadAsStreamAsync()) as Register;

                // set the tenantID
                if (register.tenantId != null)
                {
                    Connection.tenantID = register.tenantId;
                }
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

        public async Task<RegisterAsAdmin> register(LifeImageCloudConnection Connection, string username, string password, string org = null, string serviceName = null)
        {
            var taskInfo = $"username: {username}";

            var httpClient = _liteHttpClient.GetClient(Connection);

            HttpResponseMessage response = null;
            try
            {
                //set the URL
                var uri = CloudAgentConstants.RegisterUrl;// $"/api/admin/v1/agents/setup"; //?username={username}&password={password}";
                string registerURL = Connection.URL + uri;

                _logger.Log(LogLevel.Debug, $"registerURL: {registerURL}");

                //set the form parameters
                FormUrlEncodedContent registerParams = null;

                if (org == null || serviceName == null)
                {
                    registerParams = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("username", username),
                        new KeyValuePair<string, string>("password", password)
                    });
                }
                else
                {
                    registerParams = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("username", username),
                        new KeyValuePair<string, string>("password", password),
                        new KeyValuePair<string, string>("organizationCode", org),
                        new KeyValuePair<string, string>("serviceName", serviceName)
                    });
                }

                // issue the POST
                response = await httpClient.PostAsync(registerURL, registerParams, _taskManager.cts.Token);

                // output the result

                _logger.Log(LogLevel.Debug, $"result: {response.Version} {response.StatusCode} {response.ReasonPhrase}");
                foreach (var header in response.Headers)
                {
                    foreach (var value in header.Value)
                    {
                        _logger.Log(LogLevel.Debug, $"response.Headers: {header.Key} {value}");
                    }
                }

                _logger.Log(LogLevel.Debug, $"await response.Content.ReadAsStringAsync(): {response.Content.ReadAsStringAsync().Result}");
                // convert from stream to JSON

                var serializer = new DataContractJsonSerializer(typeof(RegisterAsAdmin));
                var obj = await response.Content.ReadAsStreamAsync();
                var registerAsAdmin = serializer.ReadObject(obj) as RegisterAsAdmin;
                // set the tenantID
                if (registerAsAdmin != null)
                {
                    return registerAsAdmin;
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                throw new System.Runtime.Serialization.SerializationException(response.Content.ReadAsStringAsync().Result, e);
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

            return null;
        }
    }
}
