using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ISendToAllHubsService
    {
        void Init(Dictionary<string, HubConnection> connections);
        Task<bool> SendToAllHubs(string user, string message);
    }

    public class SendToAllHubsService : ISendToAllHubsService
    {
        private readonly ILogger _logger;
        private Dictionary<string, HubConnection> hubConnections;

        public SendToAllHubsService(ILogger<SendToAllHubsService> logger)
        {
            _logger = logger;
        }

        public void Init(Dictionary<string, HubConnection> connections)
        {
            hubConnections = connections;
        }

        public async Task<bool> SendToAllHubs(string user, string message)
        {
            try
            {
                foreach (var hub in hubConnections)
                {
                    await SendToHub(user, message, hub.Value);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
                return false;
            }

            return true;
        }

        public async Task<bool> SendToHub(string user, string message, HubConnection hubConnection)
        {
            try
            {
                await hubConnection.InvokeAsync("SendMessage",
                    user, message);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
                return false;
            }

            return true;
        }
    }
}
