using Lite.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite3.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public async Task SendMessage(string user, string message)
        {
            try
            {
                await Clients.All.SendAsync("ReceiveMessage", user, message);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation($"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }
    }
}
