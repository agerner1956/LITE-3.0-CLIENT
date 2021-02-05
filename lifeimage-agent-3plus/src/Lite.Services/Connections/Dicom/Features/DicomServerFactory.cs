using Dicom.Network;
using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomServerFactory
    {
        IDicomServer CreateListener(List<IDicomServer> dicomListeners);
    }

    public sealed class DicomServerFactory : IDicomServerFactory
    {
        private readonly ILogger _logger;

        public DicomServerFactory(
            DICOMConnection connection,
            ILogger logger)
        {
            Connection = connection;
            _logger = logger;
        }

        public DICOMConnection Connection { get; private set; }

        public IDicomServer CreateListener(List<IDicomServer> dicomListeners)
        {
            var taskStatus = $"{Connection.name}:";

            // will need to handle a change at some point
            var result = dicomListeners.Find(a => a.Port == Connection.localPort);
            if (result != null)
            {
                // already exist!
                return null;
            }

            DicomServiceOptions dsoptions = new DicomServiceOptions
            {
                IgnoreSslPolicyErrors = Connection.IgnoreSslPolicyErrors,
                IgnoreUnsupportedTransferSyntaxChange = Connection.IgnoreUnsupportedTransferSyntaxChange,
                LogDataPDUs = Connection.LogDataPDUs,
                LogDimseDatasets = Connection.LogDimseDatasets,
                MaxClientsAllowed = Connection.MaxClientsAllowed,
                MaxCommandBuffer = Connection.MaxCommandBuffer,
                MaxDataBuffer = Connection.MaxDataBuffer,
                MaxPDVsPerPDU = Connection.MaxPDVsPerPDU,
                TcpNoDelay = Connection.TcpNoDelay,
                UseRemoteAEForLogName = Connection.UseRemoteAEForLogName
            };

            var hostEntry = Dns.GetHostEntry(Connection.localHostname);
            string ipaddress = null;

            foreach (var ip in hostEntry.AddressList)
            {
                //                    if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                //                  {
                ipaddress += ip.ToString() + ",";
                //                    }
            }

            _logger.Log(LogLevel.Information, $"{taskStatus} Attempting to create DICOM listener on ip {ipaddress} port {Connection.localPort}");

            //todo: send DICOM logger. WHY???
            //var dicomServer = DicomServer.Create<DicomListener>(ipaddress.TrimEnd(','), Convert.ToInt32(Connection.localPort), this, null, dsoptions, null, logger: Logger.logger);
            var dicomServer = DicomServer.Create<DicomListener>(ipaddress.TrimEnd(','), Convert.ToInt32(Connection.localPort), this, null, dsoptions, null);
            return dicomServer;
        }
    }
}
