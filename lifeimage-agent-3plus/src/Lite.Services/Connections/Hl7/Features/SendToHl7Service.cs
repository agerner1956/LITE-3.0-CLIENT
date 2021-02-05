using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Hl7.Features
{
    public interface ISendToHl7Service
    {
        Task SendToHL7(int taskID, HL7Connection connection);
    }

    public sealed class SendToHl7Service : ISendToHl7Service
    {        
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public SendToHl7Service(
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILogger<SendToHl7Service> logger)
        {            
            _taskManager = taskManager;
            _routedItemManager = routedItemManager;
            _logger = logger;
        }

        public HL7Connection Connection { get; set; }

        public async Task SendToHL7(int taskID, HL7Connection connection)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            TcpClient tcpclnt = new TcpClient(Connection.remoteHostname, Connection.remotePort);
            _logger.Log(LogLevel.Debug, $"HL7 tcpclient connection {Connection.name} opened - task = {Task.CurrentId} connected = {tcpclnt.Connected}");

            Stream stm = null;

            SslStream sslStream = null;
            try
            {
                switch (Connection.useTLS)
                {
                    case true:
                        {
                            sslStream = new SslStream(tcpclnt.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                            sslStream.AuthenticateAsClient(Connection.remoteHostname);
                            break;
                        }
                    default:
                        {
                            stm = tcpclnt.GetStream();
                            break;
                        }
                }

                var temp = Connection.ToHL7.ToArray();
                foreach (var routedItem in temp)
                {
                    try
                    {
                        await ProcessItem(routedItem, sslStream, stm, taskInfo);
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, taskInfo);                        
                    }
                }

                await Task.Delay(5000).ConfigureAwait(false); //give the socket enough time to clear.
                tcpclnt.Close();

                _logger.Log(LogLevel.Debug, "Regular HL7 tcpclient connection {name} closed - task = {Task.CurrentId} completed = {Task.CompletedTask}");

                foreach (var hl7 in temp)
                {
                    _routedItemManager.Init(hl7);
                    _routedItemManager.Dequeue(Connection, Connection.ToHL7, nameof(Connection.ToHL7), error: false);
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                switch (Connection.useTLS)
                {
                    case true:
                        sslStream.Close();
                        break;
                    default:
                        stm.Close();
                        break;
                }
                tcpclnt.Close();
                _logger.Log(LogLevel.Debug, "Final HL7 tcpclient connection {name} closed - task =  {Task.CurrentId}  completed = {Task.CompletedTask}");
            }
        }

        public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (!Connection.EnforceServerCertificate)
            {
                return true;
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }
            else
            {
                _logger.Log(LogLevel.Warning, $"{Connection.name} {sslPolicyErrors}");
                return false;
            }
        }

        private async Task ProcessItem(RoutedItem routedItem,SslStream sslStream, Stream stm, string taskInfo)
        {
            if (_taskManager.cts.IsCancellationRequested)
            {
                return;
            }

            if (routedItem.lastAttempt == null || routedItem.lastAttempt >= DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)) //not attempted lately
            {
                return;
            }

            routedItem.attempts++;
            if (routedItem.attempts > 1)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} {routedItem.sourceFileName} second attempt.");
            }

            routedItem.lastAttempt = DateTime.Now;

            if (routedItem.attempts > Connection.maxAttempts)
            {
                _logger.Log(LogLevel.Error, $"{taskInfo} {routedItem.sourceFileName} has exceeded maxAttempts of {Connection.maxAttempts}.  Will move to errors and not try again.");

                routedItem.Error = "Exceeded maxAttempts";

                _routedItemManager.Init(routedItem);
                _routedItemManager.Dequeue(Connection, Connection.ToHL7, nameof(Connection.ToHL7), error: true);
            }
            else
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} {routedItem.sourceFileName} attempts: {routedItem.attempts}");
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} Sending: {routedItem}");

            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] b1 = { 0x0B };
            byte[] b2 = { 0x1C, 0x0D };

            // add header an tail to message string

            byte[] ba = Combine(b1, asen.GetBytes(await File.ReadAllTextAsync(routedItem.sourceFileName)), b2);
            byte[] bb = new byte[600];
            int k = 0;
            switch (Connection.useTLS)
            {
                case true:
                    {
                        sslStream.Write(ba, 0, ba.Length);
                        k = sslStream.Read(bb, 0, 600);
                        break;
                    }
                default:
                    {
                        stm.Write(ba, 0, ba.Length);
                        k = stm.Read(bb, 0, 600);
                        break;
                    }
            }

            string s = System.Text.Encoding.UTF8.GetString(bb, 0, k - 1);

            _logger.Log(LogLevel.Debug, $"received: {s}");
        }

        private byte[] Combine(byte[] a1, byte[] a2, byte[] a3)
        {
            byte[] ret = new byte[a1.Length + a2.Length + a3.Length];
            Array.Copy(a1, 0, ret, 0, a1.Length);
            Array.Copy(a2, 0, ret, a1.Length, a2.Length);
            Array.Copy(a3, 0, ret, a1.Length + a2.Length, a3.Length);
            return ret;
        }
    }
}
