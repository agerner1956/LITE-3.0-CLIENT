using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Lite.Services.Connections.Hl7.Features
{
    public interface IHl7ClientsCleaner
    {
        void Clean(HL7Connection Connection, List<TcpClient> clients);
    }

    public sealed class Hl7ClientsCleaner : IHl7ClientsCleaner
    {
        private readonly ILogger _logger;

        public Hl7ClientsCleaner(ILogger<Hl7ClientsCleaner> logger)
        {
            _logger = logger;
        }

        public void Clean(HL7Connection Connection, List<TcpClient> clients)
        {
            foreach (var client in clients.ToArray())
            {
                if (!client.Connected)
                {
                    _logger.Log(LogLevel.Debug, $"{Connection.name} Disconnected");
                    client.Dispose();
                    clients.Remove(client);
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"{Connection.name} Connected: {client.Client.RemoteEndPoint}");
                }
            }
        }
    }
}
