using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud
{
    public interface ICloudKeepAliveService
    {
        Task KeepAlive(LifeImageCloudConnection Connection, IHttpManager httpManager);
    }

    public sealed class CloudKeepAliveService : ICloudKeepAliveService
    {
        private readonly ILogger _logger;
        private readonly ICloudPingService _cloudPingService;

        public CloudKeepAliveService(
            ICloudPingService cloudPingService,
            ILogger<CloudKeepAliveService> logger)
        {
            _cloudPingService = cloudPingService;
            _logger = logger;
        }

        public async Task KeepAlive(LifeImageCloudConnection Connection, IHttpManager httpManager)
        {
            var taskInfo = $"connection: {Connection.name}";

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Entering KeepAlive");

                if (httpManager.loginNeeded == false)
                {
                    if (!await _cloudPingService.Ping(Connection, httpManager))
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} ping failed");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }
    }
}
