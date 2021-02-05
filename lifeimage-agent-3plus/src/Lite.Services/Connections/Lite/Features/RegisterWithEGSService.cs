using Lite.Core.Connections;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface IRegisterWithEGSService
    {
        Task RegisterWithEGS(int taskID, LITEConnection connection, IHttpManager httpManager);
    }

    public sealed class RegisterWithEGSService : IRegisterWithEGSService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public RegisterWithEGSService(
            ILiteHttpClient liteHttpClient,
            IProfileStorage profileStorage,
            ILITETask taskManager,
            ILogger<RegisterWithEGSService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _taskManager = taskManager;
            _profileStorage = profileStorage;            
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public async Task RegisterWithEGS(int taskID, LITEConnection connection, IHttpManager httpManager)
        {
            Connection = connection;
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var httpClient = _liteHttpClient.GetClient(connection);

            try
            {
                //contact each EGS and register presence with LITEServicePoint and share dest info which will be used for address oriented routing.
                IPHostEntry hostEntry = Dns.GetHostEntry(Connection.remoteHostname);
                //look up on known dns the other LITE EGS instances and register presence
                foreach (var iPAddress in hostEntry.AddressList)
                {
                    //set the URL
                    string url = Connection.URL + "/api/LITE";
                    _logger.Log(LogLevel.Debug, $"{taskInfo} url: {url}");

                    // issue the POST
                    HttpResponseMessage response = null;

                    var cookies = _liteHttpClient.GetCookies(url);
                    _logger.LogCookies(cookies, taskInfo);

                    var profile = _profileStorage.Current;

                    string profilejson = JsonSerializer.Serialize(profile);
                    var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    writer.Write(profilejson);
                    writer.Flush();
                    stream.Position = 0;
                    StreamContent content = new StreamContent(stream);

                    var task = httpClient.PostAsync(url, content, _taskManager.cts.Token);

                    response = await task;

                    // output the result                    
                    _logger.LogHttpResponseAndHeaders(response, taskInfo);

                    _logger.Log(LogLevel.Debug, $"{taskInfo} await response.Content.ReadAsStringAsync(): {await response.Content.ReadAsStringAsync()}");

                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        connection.loginAttempts = 0;
                        _logger.Log(LogLevel.Debug, $"{taskInfo} LITE Successfully Registered with EGS!");

                        httpManager.loginNeeded = false;
                    }
                    else
                    {
                        _liteHttpClient.DumpHttpClientDetails();

                        httpManager.loginNeeded = true;
                        if (response.StatusCode == HttpStatusCode.Unauthorized && connection.loginAttempts == Connection.maxAttempts)
                        {
                            LiteEngine.shutdown(null, null);
                            Environment.Exit(0);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
            }
            catch (HttpRequestException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} HttpRequestException: Unable to login: {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} HttpRequestException: (Inner Exception) {e.InnerException.Message} {e.InnerException.StackTrace}");
                }

                _liteHttpClient.DumpHttpClientDetails();
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} Unable to login");
                _liteHttpClient.DumpHttpClientDetails();
            }
        }
    }
}
