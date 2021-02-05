using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomCMoveCommand : IDicomCommand
    {
        void CMove(RoutedItem routedItem, long taskID, DICOMConnection connection);
    }

    public sealed class DicomCMoveCommand : DicomCommandBase, IDicomCMoveCommand
    {
        private readonly IRoutedItemManager _routedItemManager;
        private readonly ILogger _logger;

        public DicomCMoveCommand(
            IRoutedItemManager routedItemManager,
            ILogger<DicomCFindCommand> logger)
        {
            _routedItemManager = routedItemManager;
            _logger = logger;
        }

        public DICOMConnection Connection { get; set; }

        public void CMove(RoutedItem routedItem, long taskID, DICOMConnection connection)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            try
            {
                try
                {
                    // 2018-01-22 shb changed from dicomServerName to localAETitle
                    var cmove = new DicomCMoveRequest(Connection.localAETitle, routedItem.request);
                    _logger.Log(LogLevel.Information, $"{taskInfo} cMove id: {routedItem.id} MessageID: {cmove.MessageID} attempt: {routedItem.attempts}");

                    cmove.UserState = routedItem;
                    routedItem.MessageId = cmove.MessageID;
                    routedItem.fromConnection = Connection.name;
                    routedItem.toConnections.Clear();
                    routedItem.status = RoutedItem.Status.PENDING;

                    _routedItemManager.Init(routedItem);
                    //var riToCache = (RoutedItem)routedItem.Clone();
                    var riToCache = _routedItemManager.Clone();
                    _routedItemManager.Init(riToCache);
                    _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                    // Returns the status of the request, actual transfer happens in DicomListener
                    cmove.OnResponseReceived = (DicomCMoveRequest request, DicomCMoveResponse response) =>
                    {
                        RoutedItem ri = (RoutedItem)request.UserState;

                        _logger.Log(LogLevel.Information, $"{taskInfo} cmove.OnResponseReceived id: {ri.id} MessageId: {request.MessageID} {response.Status.Description}");

                        Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>();

                        Dictionary<string, string> returnTagData = new Dictionary<string, string>
                        {
                            { "StatusCode", response.Status.Code.ToString() },
                            { "StatusDescription", response.Status.Description },
                            { "StatusErrorComment", response.Status.ErrorComment },
                            { "StatusState", response.Status.State.ToString() }
                        };

                        if (response.Completed != 0 || response.Remaining != 0)
                        {
                            returnTagData.Add("Completed", response.Completed.ToString());
                            returnTagData.Add("Remaining", response.Remaining.ToString());
                        }
                        returnTagData.Add("SOPClassUID", response.SOPClassUID.ToString());

                        if (response.Command != null)
                        {
                            foreach (var dicomItem in response.Command)
                            {
                                _logger.Log(LogLevel.Debug, $"{taskInfo} cMove Response {ri.id} MessageId {request.MessageID}, {dicomItem.Tag} {response.Command.GetValueOrDefault<string>(dicomItem.Tag, 0, "")}");
                                returnTagData.Add(dicomItem.Tag.ToString(), response.Command.GetValueOrDefault<string>(dicomItem.Tag, 0, ""));

                            }
                            results.Add("response", returnTagData);
                        }

                        string jsonResults = JsonSerializer.Serialize(results);

                        ri.response.Add(jsonResults);

                        switch (response.Status.ToString())
                        {
                            case "Success":
                                ri.status = RoutedItem.Status.COMPLETED;
                                ri.resultsTime = DateTime.Now;
                                _routedItemManager.Init(ri);
                                _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: false);

                                //var toCache = (RoutedItem)ri.Clone();
                                var toCache = _routedItemManager.Clone();
                                toCache.fromConnection = Connection.name;
                                ri.toConnections.Clear();

                                _routedItemManager.Init(toCache);
                                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
                                break;
                            case "Pending":
                                break;

                            default:
                                _routedItemManager.Init(ri);
                                _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: true);
                                break;
                        }
                    };

                    dicomClient.AddRequest(cmove);
                }
                catch (Exception e)   // Needed for transfer syntax exceptions
                {
                    _logger.LogFullException(e, $"{taskInfo} move:");  
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }
    }
}
