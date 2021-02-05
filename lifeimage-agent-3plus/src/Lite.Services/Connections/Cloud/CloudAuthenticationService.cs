using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Lite.Services.Http;
using Lite.Core.Security;

namespace Lite.Services.Connections.Cloud
{
    public interface ICloudLoginService
    {
        Task<string> login(int taskID, LifeImageCloudConnection connection, IHttpManager manager);
    }

    public interface ICloudLogoutService
    {
        Task logout(int taskID, LifeImageCloudConnection connection, IHttpManager manager);
    }

    public sealed class CloudAuthenticationService : ICloudLoginService, ICloudLogoutService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;
        private readonly ICrypto _crypto;

        public CloudAuthenticationService(
            ILiteHttpClient liteHttpClient,
            ILITETask taskManager,
            ICrypto crypto,
            ILogger<CloudAuthenticationService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _crypto = crypto;
            _logger = logger;
        }

        public LifeImageCloudConnection Connection { get; private set; }

        public async Task<string> login(int taskID, LifeImageCloudConnection connection, IHttpManager _manager)
        {
            Connection = connection;
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var httpClient = _liteHttpClient.GetClient(connection);

            try
            {
                Connection.loginAttempts++;

                //set the URL
                string loginURL = Connection.URL + "/login/authenticate";
                _logger.Log(LogLevel.Debug, $"{taskInfo} loginURL: {loginURL}");

                //set the form parameters
                var loginParams = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("j_username", Connection.username),
                    new KeyValuePair<string, string>("j_password", GetUnprotectedPassword(connection)),
                    new KeyValuePair<string, string>("OrganizationCode", Connection.organizationCode),
                    new KeyValuePair<string, string>("ServiceName", Connection.serviceName),
                    new KeyValuePair<string, string>("applTenantId", Connection.tenantID)
                });

                // issue the POST
                HttpResponseMessage response = null;
                try
                {
                    var cookies = _liteHttpClient.GetCookies(loginURL);
                    _logger.LogCookies(cookies, taskInfo);

                    var task = httpClient.PostAsync(loginURL, loginParams, _taskManager.cts.Token);

                    //ServicePointManager.FindServicePoint(new Uri(loginURL)).ConnectionLeaseTimeout = 0; //(int)TimeSpan.FromMinutes(1).TotalMilliseconds;

                    response = await task;
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
                    return null;
                }
                catch (HttpRequestException e)
                {
                    _logger.LogFullException(e, $"{taskInfo} HttpRequestException: Unable to login:");
                    _liteHttpClient.DumpHttpClientDetails();

                    return e.ToString();
                }
                catch (Exception e)
                {
                    _logger.LogFullException(e, $"{taskInfo} HttpRequestException: Unable to login:");
                    _liteHttpClient.DumpHttpClientDetails();

                    return e.ToString();
                }

                // output the result
                _logger.LogHttpResponseAndHeaders(response, taskInfo);                                
                _logger.Log(LogLevel.Debug, $"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Connection.loginAttempts = 0;
                    _logger.Log(LogLevel.Debug, $"{taskInfo} User Successfully logged in!");

                    _manager.loginNeeded = false;
                }
                else
                {
                    _liteHttpClient.DumpHttpClientDetails();

                    _manager.loginNeeded = true;
                    if (response.StatusCode == HttpStatusCode.Unauthorized && Connection.loginAttempts == Connection.maxAttempts)
                    {
                        Console.WriteLine("Exceeded max login attempts. Shutting down");
                        LiteEngine.shutdown(this, null);
                        Environment.Exit(0);
                    }
                }

                // grab the X-Li-Synctoken
                if (response.Headers.TryGetValues("X-Li-Synctoken", out IEnumerable<string> syncTokens))
                {
                    foreach (var token in syncTokens)
                    {
                        Connection.syncToken = token;
                        httpClient.DefaultRequestHeaders.Remove("X-Li-Synctoken"); //in case we have to login again without recreating httpClient
                        httpClient.DefaultRequestHeaders.Add("X-Li-Synctoken", Connection.syncToken);
                        break;
                    }
                }

                // get the session cookie
                var newcookies = _liteHttpClient.GetCookies(loginURL);
                foreach (var cookie in newcookies)
                {
                    var cookiestr = cookie.ToString();
                    if (cookiestr.Contains("JSESSIONID"))
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Cookie: {cookiestr}");
                        _manager.jSessionID = cookiestr;
                    }
                }

                _logger.Log(LogLevel.Debug, $"{taskInfo} Login successful: {_manager.jSessionID}");

                // convert from stream to JSON
                //BUG JSON is invalid as of 4/11/2016
                //var serializer = new DataContractJsonSerializer(typeof(LoginJSON));
                //var loginJSON = serializer.ReadObject(await response.Content.ReadAsStreamAsync()) as LoginJSON;


                return response.StatusCode.ToString();
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);               
                return null;
            }
        }

        private string GetUnprotectedPassword(Connection connection)
        {
            if (connection.password == null || connection.sharedKey == null || connection.IV == null)
            {
                return string.Empty;
            }
            else
            {
                var temp = _crypto.Unprotect(connection.password,
                    Convert.FromBase64String(connection.sharedKey),
                    Convert.FromBase64String(connection.IV));

                return temp;
            }
        }

        // logout
        public async Task logout(int taskID, LifeImageCloudConnection connection, IHttpManager _manager)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var httpClient = _liteHttpClient.GetClient(connection);

            try
            {
                //set the URL
                string logoutURL = Connection.URL + "/logout";
                _logger.Log(LogLevel.Debug, $"{taskInfo} logoutURL: {logoutURL}");

                var cookies = _liteHttpClient.GetCookies(logoutURL);
                _logger.LogCookies(cookies, taskInfo);

                // issue the GET
                var task = httpClient.GetAsync(logoutURL, _taskManager.cts.Token);
                var response = await task;

                // output the result                                
                _logger.LogHttpResponseAndHeaders(response, taskInfo);
                _logger.Log(LogLevel.Debug, $"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                // convert from stream to JSON
                //BUG JSON is invalid as of 4/11/2016
                //var serializer = new DataContractJsonSerializer(typeof(LoginJSON));
                //var loginJSON = serializer.ReadObject(await response.Content.ReadAsStreamAsync()) as LoginJSON;

                //clear out the jSessionID and syncTokens
                _manager.jSessionID = null;
                Connection.syncToken = null;

                httpClient.Dispose();
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
            }
        }
    }
}
