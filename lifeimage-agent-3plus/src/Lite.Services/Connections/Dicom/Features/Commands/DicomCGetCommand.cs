using Dicom;
using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomCGetCommand : IDicomCommand
    {
        void CGet(RoutedItem routedItem, int taskID, DICOMConnection connection);
    }

    public sealed class DicomCGetCommand : DicomCommandBase, IDicomCGetCommand
    {
        private readonly ILogger _logger;

        public DicomCGetCommand(
            ILogger<DicomCGetCommand> logger)
        {
            _logger = logger;
        }

        public DICOMConnection Connection { get; set; }

        // Note: cGet is not working at this point
        public void CGet(RoutedItem routedItem, int taskID, DICOMConnection connection)
        {
            Connection = connection;
            //process any outstanding moves and return the results

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            List<string> movesToRemove = new List<string>();

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} dicom.AddRequest: {routedItem.id} ");
                try
                {
                    var request = new DicomCGetRequest(routedItem.request)
                    {
                        UserState = routedItem
                    };

                    request.OnResponseReceived += CGetResponse;
                    // Fix if cGet is ever fixed                        dicomClient.AddRequest(request);
                }
                catch (TaskCanceledException)
                {
                    _logger.Log(LogLevel.Information, $"Task was canceled.");
                }
                catch (DicomDataException e)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} move: {routedItem.id} {e.Message} {e.StackTrace}");
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }

        private void CGetResponse(DicomCGetRequest request, DicomCGetResponse response)
        {
            RoutedItem ri = (RoutedItem)request.UserState;

            var taskInfo = $"id: {ri.id} messageID: {request.MessageID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} response: {response.Status}");

            try
            {
                //if(response.Status.ToString().Equals("Success") || response.Status.ToString().Equals("Pending")){

                if (response.HasDataset)
                {
                    foreach (var data in response.Dataset)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} response.dataset.tag: {data}  ");
                    }
                    _logger.Log(LogLevel.Debug, $"{taskInfo} StudyID: {response.Dataset.GetValue<string>(DicomTag.StudyID, 0)} ");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} StudyInstanceUID: {response.Dataset.GetValue<string>(DicomTag.StudyInstanceUID, 0)} ");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} SOPInstanceUID: {response.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0)} ");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} QueryRetrieveLevel: {response.Dataset.GetValue<string>(DicomTag.QueryRetrieveLevel, 0)} ");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} PatientName: {response.Dataset.GetValue<string>(DicomTag.PatientName, 0)} ");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} PatientBirthDate: {response.Dataset.GetValue<string>(DicomTag.PatientBirthDate, 0)} ");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} StudyDate: {response.Dataset.GetValue<string>(DicomTag.StudyDate, 0)} ");
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }
    }
}
