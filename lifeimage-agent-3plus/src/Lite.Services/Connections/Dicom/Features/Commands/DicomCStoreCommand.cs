using Dicom;
using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomCStoreCommand : IDicomCommand
    {
        /// <summary>
        /// Push a file to DICOM.
        /// </summary>
        /// <param name="routedItem"></param>
        /// <param name="taskID"></param>
        /// <param name="connection"></param>
        void CStore(RoutedItem routedItem, long taskID, DICOMConnection connection);
    }

    public sealed class DicomCStoreCommand : DicomCommandBase, IDicomCStoreCommand
    {        
        private readonly IRoutedItemManager _routedItemManager;        
        private readonly ILogger _logger;

        public DicomCStoreCommand(            
            IRoutedItemManager routedItemManager,            
            ILogger<DicomCStoreCommand> logger)
        {            
            _routedItemManager = routedItemManager;            
            _logger = logger;
        }

        public DICOMConnection Connection { get; set; }

        /// <summary>
        /// Push a file to DICOM.
        /// </summary>
        /// <param name="routedItem"></param>
        /// <param name="taskID"></param>
        /// <param name="connection"></param>
        public void CStore(RoutedItem routedItem, long taskID, DICOMConnection connection)
        {
            Connection = connection;
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                try
                {
                    if (File.Exists(routedItem.sourceFileName))
                    {
                        var cStoreRequest = new DicomCStoreRequest(routedItem.sourceFileName);
                        routedItem.MessageId = cStoreRequest.MessageID;
                        cStoreRequest.UserState = routedItem;
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Request id: {routedItem.id} {cStoreRequest.MessageID}, {routedItem.sourceFileName} {routedItem.fileIndex}/{routedItem.fileCount} attempt: {routedItem.attempts}");

                        cStoreRequest.OnResponseReceived = (DicomCStoreRequest request, DicomCStoreResponse response) =>
                        {
                            var ri = (RoutedItem)request.UserState;
                            _logger.Log(LogLevel.Information, $"{taskInfo} Request id: {ri.id} {response.RequestMessageID} status: {response.Status.Code} description: {response.Status.Description} comment: {response.Status.ErrorComment} state: {response.Status.State}");

                            _routedItemManager.Init(ri);
                            //2018-08-01 shb BOUR-559 handle and log failure conditions
                            if (response.Status == DicomStatus.Success)
                            {
                                _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: false);
                            }
                            else
                            {
                                _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: true);
                            }

                            OnCStoreRequestComplete(request, response);
                        };

                        dicomClient.AddRequest(cStoreRequest);
                    }
                    else
                    {
                        _logger.Log(LogLevel.Information, $"{taskInfo} File does not exist: {routedItem.sourceFileName}");
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: true);
                    }
                }
                catch (DicomDataException e)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} {routedItem.sourceFileName} {e.Message} {e.StackTrace} ");

                    // !e.GetType().IsAssignableFrom(typeof(DicomFileException)))

                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: true);
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }

        public void OnCStoreRequestComplete(DicomCStoreRequest request, DicomCStoreResponse response)
        {
            _logger.Log(LogLevel.Information, $"MessageID: {response.RequestMessageID} status: {response.Status.Code} description: {response.Status.Description} comment: {response.Status.ErrorComment} state: {response.Status.State}");
        }
    }
}
