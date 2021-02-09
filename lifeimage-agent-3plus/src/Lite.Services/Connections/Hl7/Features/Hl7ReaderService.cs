using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Hl7.Features
{
    public interface IHl7ReaderService
    {
        Task Read(TcpClient client, int taskID, HL7Connection connection);
    }

    public sealed class Hl7ReaderService : IHl7ReaderService
    {
        private readonly IAckMessageFormatter _ackMessageFormatter;
        private readonly IProfileStorage _profileStorage;
        private readonly ILogger _logger;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IX509CertificateService _x509CertificateService;
        private readonly ILITETask _taskManager;

        public Hl7ReaderService(
            IAckMessageFormatter ackMessageFormatter,
             IX509CertificateService x509CertificateService,
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILogger<Hl7ReaderService> logger)
        {
            _ackMessageFormatter = ackMessageFormatter;
            _profileStorage = profileStorage;
            _logger = logger;
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _x509CertificateService = x509CertificateService;
        }

        public HL7Connection Connection { get; set; }

        public async Task Read(TcpClient client, int taskID, HL7Connection connection)
        {
            Connection = connection;
            Stopwatch idle = new Stopwatch();
            var taskInfo = $"task: {taskID} connection: {Connection.name} {client?.Client?.RemoteEndPoint}";
            Stream stream = null;
            try
            {
                NetworkStream networkStream = client.GetStream();

                if (Connection.useTLS)
                {
                    SslStream ssls = new SslStream(networkStream, false);
                    ssls.AuthenticateAsServer(_x509CertificateService.GetServerCertificate(Connection.name), false, SslProtocols.Tls12, true);
                    stream = ssls;
                }
                else
                {
                    stream = networkStream;
                }

                if (client.Connected)
                {
                    //read the stream if avail, acknowledge, throw into hl7 receive array for other async processing
                    do
                    {
                        byte[] buffer = new byte[client.ReceiveBufferSize];
                        if (stream.CanRead)
                        {
                            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _taskManager.cts.Token);
                            if (bytesRead > 0)
                            {
                                idle.Stop();
                                string controlId = "";
                                byte[] tmp = new byte[bytesRead];
                                Array.Copy(buffer, tmp, bytesRead);

                                var dir = _profileStorage.Current.tempPath +
                                    Path.DirectorySeparatorChar +
                                    Connection.name +
                                    Path.DirectorySeparatorChar +
                                    Constants.Dirs.ToRules;

                                Directory.CreateDirectory(dir);
                                var filename = dir + Path.DirectorySeparatorChar + Connection.name + "_" +
                                               DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss.ffff") + ".hl7";
                                await File.WriteAllBytesAsync(filename, buffer);
                                RoutedItem routedItem = new RoutedItem(Connection.name, filename, taskID)
                                {
                                    type = RoutedItem.Type.HL7
                                };

                                string msg = Encoding.Default.GetString(tmp);
                                if (msg.StartsWith('\v'))
                                {
                                    msg = msg.Substring(1);
                                }

                                if (msg.Length >= 4 && msg.StartsWith("MSH"))
                                {
                                    List<KeyValuePair<string, List<string>>> segments = new List<KeyValuePair<string, List<string>>>();
                                    List<string> temp = null;
                                    if (msg.Contains("\r\n"))
                                    {
                                        temp = msg.Split("\r\n").ToList();
                                    }
                                    else if (msg.Contains("\n"))
                                    {
                                        temp = msg.Split("\n").ToList();
                                    }
                                    else if (msg.Contains("\r"))
                                    {
                                        temp = msg.Split("\r").ToList();
                                    }
                                    else
                                    {
                                        _logger.Log(LogLevel.Error, $"Unknown segment delimiter");
                                    }

                                    foreach (var segment in temp)
                                    {
                                        if (segment.Length >= 4)
                                        {
                                            segments.Add(new KeyValuePair<string, List<string>>(segment.Substring(0, 3),
                                                segment.Split((char)msg[3]).ToList()));
                                        }
                                    }

                                    routedItem.hl7 = segments;

                                    //string[] fields = msg.Split((char)tmp[4]);
                                    var msh = segments.Find(e => e.Key == "MSH");
                                    if (msh.Value.Count >= 10)
                                    {
                                        controlId = msh.Value[9];
                                    }


                                    //determine the patientID and accession number and populate the ID field
                                    try
                                    {
                                        var pid = segments.Find(e => e.Key == "PID");
                                        if (pid.Value[3].IndexOf("^") > 0)
                                        {
                                            routedItem.PatientID = pid.Value[3].Substring(0, pid.Value[3].IndexOf("^"));
                                        }
                                        else
                                        {
                                            routedItem.PatientID = pid.Value[3];
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.Log(LogLevel.Critical, $"{taskInfo} Error Parsing PID: {e.Message} {e.StackTrace}");
                                    }

                                    try
                                    {
                                        var obr = segments.Find(e => e.Key == "OBR");
                                        if (obr.Value[3].IndexOf("^") > 0)
                                        {
                                            routedItem.AccessionNumber =
                                                obr.Value[3].Substring(0, obr.Value[3].IndexOf("^"));
                                        }
                                        else
                                        {
                                            routedItem.AccessionNumber = obr.Value[3];
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.Log(LogLevel.Critical,
                                            $"{taskInfo} Error Parsing OBR: {e.Message} {e.StackTrace}");
                                    }

                                    routedItem.id = $"PID:{routedItem.PatientID}, AN:{routedItem.AccessionNumber}"; //, UID:{routedItem.Study}";
                                }

                                _routedItemManager.Init(routedItem);
                                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                                _logger.Log(LogLevel.Debug, $"{taskInfo} {msg}");

                                byte[] serverResponseBytes = null;
                                switch (Connection.ackMode)
                                {
                                    case AckMode.Original:
                                        {
                                            //process message and return response bytes
                                            break;
                                        }
                                    case AckMode.Enhanced:
                                        {
                                            //process message and return response bytes
                                            break;
                                        }
                                    case AckMode.Custom:
                                        {
                                            serverResponseBytes = Encoding.UTF8.GetBytes(GetAckMessage(controlId));
                                            break;
                                        }
                                }

                                if (serverResponseBytes != null)
                                {
                                    await stream.WriteAsync(serverResponseBytes, 0, serverResponseBytes.Length);
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} {Encoding.Default.GetString(serverResponseBytes)}");
                                }
                                else
                                {
                                    _logger.Log(LogLevel.Debug, $"{taskInfo} serverResponseBytes is null.  Please verify ackMode and ack values.");
                                }
                            }

                            //await stream.FlushAsync();
                        }
                        else
                        {
                            break;
                        }

                    } while (networkStream.DataAvailable);
                    // await Task.Delay(10000);  //if no data avail then sleep on socke
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
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }

                if (client != null)
                {
                    client.Close();
                    _logger.Log(LogLevel.Debug, "Inbound HL7 tcpclient connection {name} closed - task = {Task.CurrentId} completed = {Task.CompletedTask}");
                }

                // note: do not call this logic inside method. It should be outside
                //Clean();   // cleanup the connection, remove from list
                //LITETask.Stop($"{Connection.name}.read");
            }
        }

        private string GetAckMessage(string controlId)
        {
            return _ackMessageFormatter.GetAckMessage(Connection, controlId);
        }
    }
}
