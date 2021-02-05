using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Hl7.Features
{
    public interface IHl7AcceptService
    {
        Task Accept(HL7Connection connection, BourneListens bourne, List<TcpClient> clients, Func<TcpClient, int, Task> func, int taskID);
    }

    public sealed class Hl7AcceptService : IHl7AcceptService
    {
        private readonly ILogger _logger;
        private readonly ILITETask _taskManager;

        public Hl7AcceptService(
            ILITETask taskManager,
            ILogger<Hl7AcceptService> logger)
        {
            _logger = logger;
            _taskManager = taskManager;
        }

        public HL7Connection Connection { get; set; }

        public async Task Accept(HL7Connection connection, BourneListens bourne, List<TcpClient> clients, Func<TcpClient, int, Task> func, int taskID)
        {
            Connection = connection;

            _logger.Log(LogLevel.Debug, $"{Connection.name} Entering accept on {bourne.LocalEndpoint}");

            string local = string.Empty;
            try
            {
                while (!_taskManager.cts.IsCancellationRequested)
                {
                    if (!bourne.Active())
                    {
                        break;
                    }

                    local = await ProcessItem(bourne, clients, func, taskID);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (SocketException e)
            {
                _logger.Log(LogLevel.Warning, $"{local} SocketException: {e.ErrorCode} {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }
            }
            catch (ObjectDisposedException e)
            {
                _logger.Log(LogLevel.Warning, $"{local} HL7 Connection is closed.");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }
            }
            catch (System.InvalidOperationException e)
            {
                _logger.Log(LogLevel.Warning, $"{local} InvalidOperationException: {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, local);
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.accept: {bourne.LocalEndpoint}");
            }

            _logger.Log(LogLevel.Warning, $"Cancellation received. Exiting accept.");
        }

        private async Task<string> ProcessItem(BourneListens bourne, List<TcpClient> clients, Func<TcpClient, int, Task> func, int taskID)
        {
            //You can echo "Hello world!" | nc ::1 2575 to send messages to accept a new connection request
            TcpClient client = await bourne.AcceptTcpClientAsync();

            string remote = client.Client.RemoteEndPoint.ToString();
            var local = bourne.LocalEndpoint.ToString();

            _logger.Log(LogLevel.Debug, $"{local} connected to: {remote}");

            if (clients.Count < Connection.maxInboundConnections && _taskManager.CanStart($"{Connection.name}.read"))
            {
                clients.Add(client);
                _logger.Log(LogLevel.Debug, $"{local} added to clients: {remote}");
                var endpoint = client.Client.RemoteEndPoint;
                var newTaskID = _taskManager.NewTaskID();
                Task task = new Task(new Action(async () => await func(client, newTaskID)), _taskManager.cts.Token);
                await _taskManager.Start(newTaskID, task, $"{Connection.name}.read", $"{Connection.name}.read: {remote}", isLongRunning: false);
            }
            else
            {
                _logger.Log(LogLevel.Warning, $"{local} Max Connections: {Connection.maxInboundConnections} reached.");
                _logger.Log(LogLevel.Debug, $"client connection closed {taskID} due to max connections reached");
                client.Close();
            }

            return local;
        }
    }
}
