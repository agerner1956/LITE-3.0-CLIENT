using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomCEchoCommand : IDicomCommand
    {
        Task CEcho(DICOMConnection Connection, int taskID);
    }

    public sealed class DicomCEchoCommand : DicomCommandBase, IDicomCEchoCommand
    {
        private readonly ILogger _logger;
        public DicomCEchoCommand(ILogger<DicomCEchoCommand> logger)
        {
            _logger = logger;
        }

        public async Task CEcho(DICOMConnection Connection, int taskID)
        {
            Throw.IfNull(Connection);

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            //just c-echo the host to say hello

            dicomClient.AddRequest(new DicomCEchoRequest());

            DicomSendRequestService dicomSendRequestService = new DicomSendRequestService(dicomClient, Connection, _logger);

            try
            {
                await dicomSendRequestService.SendRequest(taskInfo, stopWatch);

                #region old code
                //await Task.Run(async () =>
                //{
                //    await dicomClient.SendAsync(Connection.remoteHostname, Connection.remotePort, Connection.useTLS, Connection.localAETitle, Connection.remoteAETitle);

                //    _logger.Log(LogLevel.Debug, $"{taskInfo} SendAsync complete elapsed: {stopWatch.Elapsed}");

                //    await Task.Delay(Connection.msToWaitAfterSendBeforeRelease).ConfigureAwait(false);

                //    _logger.Log(LogLevel.Debug, $"{taskInfo} Releasing: {stopWatch.Elapsed}");

                //    await dicomClient.ReleaseAsync();
                //});
                #endregion
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (AggregateException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} SendAsync: {e.Message} {e.StackTrace}");

                foreach (Exception exp in e.InnerExceptions)
                {
                    if (exp != null && exp.Message != null)
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} Inner Exception: {exp.Message} {exp.StackTrace}");
                    }
                }
            }
            catch (DicomAssociationRejectedException e)
            {
                foreach (var context in dicomClient.AdditionalPresentationContexts)
                {
                    if (!(context.Result == DicomPresentationContextResult.Accept))
                        _logger.Log(LogLevel.Warning, "Not Accepted: " + context.GetResultDescription());
                }

                _logger.Log(LogLevel.Warning, $"{taskInfo} SendAsync: {e.Message} {e.StackTrace}");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} SendAsync:");
            }
        }
    }
}
