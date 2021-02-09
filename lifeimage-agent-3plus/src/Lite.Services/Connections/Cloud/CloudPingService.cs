using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud
{
    public interface ICloudPingService
    {
        Task<bool> Ping(LifeImageCloudConnection Connection, IHttpManager httpManager);
    }

    public sealed class CloudPingService : ICloudPingService
    {
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILogger _logger;

        public CloudPingService(
            ILiteHttpClient liteHttpClient,
            ILogger<CloudPingService> logger)
        {
            _liteHttpClient = liteHttpClient;
            _logger = logger;
        }        

        public async Task<bool> Ping(LifeImageCloudConnection Connection, IHttpManager httpManager)
        {
            Throw.IfNull(Connection);
            Throw.IfNull(httpManager);

            var taskInfo = $"connection: {Connection.name}";

            var httpClient = _liteHttpClient.GetClient(Connection);

            try
            {
                if (httpClient != null)
                {
                    try
                    {
                        var task = httpClient.GetAsync(Connection.URL + CloudAgentConstants.PingUrl);
                        var result = await task;

                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            return true;
                        }
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
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);                
            }

            return false;
        }
    }
}
