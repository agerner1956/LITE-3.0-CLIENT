using Lite.Core.Connections;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILitePingService
    {
        Task<bool> ping(LITEConnection connection, IHttpManager httpManager);
    }

    public sealed class LitePingService : ILitePingService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILogger _logger;

        public LitePingService(
            ILiteHttpClient liteHttpClient,
            ILogger<LitePingService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public async Task<bool> ping(LITEConnection connection, IHttpManager httpManager)
        {
            var taskInfo = $"connection: {Connection.name}";
            var httpClient = _liteHttpClient.GetClient(connection);

            try
            {
                if (httpClient != null)
                {
                    try
                    {
                        //var task = httpClient.GetAsync(Connection.URL + "/api/LITE");
                        var task = httpClient.GetAsync(Connection.URL + LiteAgentConstants.BaseUrl);
                        var result = await task;

                        if (result.StatusCode == HttpStatusCode.OK) return true;
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, $"{taskInfo} ping failed:");
                        _liteHttpClient.DumpHttpClientDetails();
                    }

                    _logger.Log(LogLevel.Warning, $"{taskInfo} ping failed: setting loginNeeded = true");
                }

                httpManager.loginNeeded = true;
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);                
            }

            return false;
        }
    }
}
