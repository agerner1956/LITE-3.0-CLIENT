using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Hl7.Features
{
    public interface IHl7StartService
    {
        Task Start(HL7Connection Connection, List<BourneListens> listeners, ObservableCollection<BourneListens> deadListeners, List<TcpClient> clients, Func<TcpClient, int, Task> Read);
    }

    public sealed class Hl7StartService : IHl7StartService
    {
        private readonly IHl7AcceptService _hl7AcceptService;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public Hl7StartService(
            IHl7AcceptService hl7AcceptService,
            ILITETask taskManager,
            ILogger<Hl7StartService> logger)
        {
            _hl7AcceptService = hl7AcceptService;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task Start(HL7Connection Connection, List<BourneListens> listeners, ObservableCollection<BourneListens> deadListeners, List<TcpClient> clients, Func<TcpClient, int, Task> Read)
        {
            try
            {
                await StartImpl(Connection, listeners, deadListeners, clients, Read);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (SocketException e)
            {
                _logger.Log(LogLevel.Warning, $"{e.Message} {e.StackTrace}");
            }
            catch (ObjectDisposedException e)
            {
                _logger.LogFullException(e);
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }

        /// <summary>
        /// check existing listeners and remove if dead.
        /// </summary>
        private void RemoveDeadListeners(List<BourneListens> listeners, ObservableCollection<BourneListens> deadListeners)
        {
            //check existing listeners and remove if dead
            foreach (var bourne in listeners)
            {
                if (!bourne.Active())
                {
                    deadListeners.Add(bourne);
                    try
                    {
                        bourne.Stop();
                    }
                    catch (Exception) { }
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"{bourne.LocalEndpoint} Active: {bourne.Active()} Connections Pending: {bourne.Pending()}");
                }
            }

            foreach (var bourne in deadListeners)
            {
                listeners.Remove(bourne);
            }
        }

        private async Task StartImpl(HL7Connection Connection, List<BourneListens> listeners, ObservableCollection<BourneListens> deadListeners, List<TcpClient> clients, Func<TcpClient, int, Task> Read)
        {
            RemoveDeadListeners(listeners, deadListeners);

            //frequent DNS lookup required for Cloud and HA environments where a lower TTL results in faster failover.
            //For a listener this means a container might have been moved to another server with different IP.
            var hostEntry = Dns.GetHostEntry(Connection.localHostname);

            foreach (var ip in hostEntry.AddressList)
            {
                _logger.Log(LogLevel.Information, $"{Connection.name} hostEntry: {Connection.localHostname} ip: {ip}");
                BourneListens bourne = null;
                if (ip.AddressFamily == AddressFamily.InterNetworkV6 && Connection.UseIPV6)
                {
                    if (!listeners.Exists(x => x.localaddr.Equals(ip) && x.port.Equals(Connection.localPort)))
                    {
                        bourne = new BourneListens(ip, Connection.localPort);
                        listeners.Add(bourne);

                        //you can verify Start worked on mac by doing lsof -n -i:2575 | grep LISTEN, where 2575 is HL7 or whatever port you want.
                        bourne.Start();
                        _logger.Log(LogLevel.Information, $"{Connection.name} is listening on {bourne.LocalEndpoint}");
                        _logger.Log(LogLevel.Information, $"{Connection.name} Verify with Mac/Linux:lsof -n -i:{Connection.localPort} | grep LISTEN and with echo \"Hello world\" | nc {Connection.localHostname} {Connection.localPort}");
                        _logger.Log(LogLevel.Information, $"{Connection.name} Verify with Windows:netstat -abno (requires elevated privileges)");
                    }
                }

                if ((ip.AddressFamily == AddressFamily.InterNetwork && Connection.UseIPV4 && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    || (ip.AddressFamily == AddressFamily.InterNetwork && Connection.UseIPV4 && !Connection.UseIPV6 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
                {
                    if (!listeners.Exists(x => x.localaddr.Equals(ip) && x.port.Equals(Connection.localPort)))
                    {
                        bourne = new BourneListens(ip, Connection.localPort);
                        listeners.Add(bourne);

                        //you can verify Start worked on mac by doing lsof -n -i:2575 | grep LISTEN, where 2575 is HL7 or whatever port you want.
                        bourne.Start();
                        _logger.Log(LogLevel.Information, $"{Connection.name} is listening on {bourne.LocalEndpoint}");
                        _logger.Log(LogLevel.Information, $"{Connection.name} Verify with Mac/Linux:lsof -n -i:{Connection.localPort} | grep LISTEN and with echo \"Hello world\" | nc {Connection.localHostname} {Connection.localPort}");
                        _logger.Log(LogLevel.Information, $"{Connection.name} Verify with Windows:netstat -abno (requires elevated privileges)");
                    }
                }
            }

            foreach (var listener in listeners)
            {
                if (listener != null && listener.LocalEndpoint != null)
                {
                    if (_taskManager.CanStart($"{Connection.name}.accept: {listener.LocalEndpoint}"))
                    {
                        var newTaskID = _taskManager.NewTaskID();
                        Task task = new Task(new Action(async () => await _hl7AcceptService.Accept(Connection, listener, clients, Read, newTaskID)), _taskManager.cts.Token);
                        await _taskManager.Start(newTaskID, task, $"{Connection.name}.accept: {listener.LocalEndpoint}", $"{Connection.name}.accept: {listener.LocalEndpoint}", isLongRunning: true);
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"listener is disposed but still in list. Ignoring");
                }
            }
        }
    }
}
