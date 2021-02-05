using Dicom.Network;
using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dicom.Features
{
    public class DicomSendRequestService
    {
        private readonly ILogger _logger;
        private readonly DicomClient _dicomClient;

        public DicomSendRequestService(
            DicomClient dicomClient,
            DICOMConnection connection,
            ILogger logger)
        {
            _dicomClient = dicomClient;
            _logger = logger;

            Connection = connection;
        }

        public DICOMConnection Connection { get; private set; }

        public async Task SendRequest(string taskInfo, Stopwatch stopWatch)
        {
            await Task.Run(async () =>
            {
                await SendRequestImpl(taskInfo, stopWatch);
            });
        }

        private async Task SendRequestImpl(string taskInfo, Stopwatch stopWatch)
        {
            await _dicomClient.SendAsync(Connection.remoteHostname, Connection.remotePort, Connection.useTLS, Connection.localAETitle, Connection.remoteAETitle);

            _logger.Log(LogLevel.Debug, $"{taskInfo} SendAsync complete elapsed: {stopWatch.Elapsed}");

            await Task.Delay(Connection.msToWaitAfterSendBeforeRelease).ConfigureAwait(false);

            _logger.Log(LogLevel.Debug, $"{taskInfo} Releasing: {stopWatch.Elapsed}");

            await _dicomClient.ReleaseAsync();
        }
    }
}
