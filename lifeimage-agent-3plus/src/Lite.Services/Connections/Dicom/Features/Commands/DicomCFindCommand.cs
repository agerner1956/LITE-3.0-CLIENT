using Dicom;
using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services.Connections.Dicom.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomCFindCommand : IDicomCommand
    {
        void CFind(RoutedItem routedItem, DICOMConnection Connection, int taskID);
    }

    public sealed class DicomCFindCommand : DicomCommandBase, IDicomCFindCommand
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IConnectionFinder _connectionFinder;
        private readonly ILogger _logger;

        public DicomCFindCommand(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IConnectionFinder connectionFinder,
            ILogger<DicomCFindCommand> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _connectionFinder = connectionFinder;
            _logger = logger;
        }

        public void CFind(RoutedItem routedItem, DICOMConnection Connection, int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            try
            {
                LiCloudRequest cFindParams = LiCloudRequest.FromJson(routedItem.request, _logger);
                foreach (var tag in cFindParams.searchTags)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} id: {routedItem.id} tag: {tag.Key} {tag.Value}");
                }

                DicomCFindRequest cFind = null;
                string cfindlevel = "";
                cFindParams.searchTags.TryGetValue("0008,0052", out cfindlevel);

                switch (cfindlevel)
                {
                    case "SERIES":
                        cFind = new DicomCFindRequest(DicomQueryRetrieveLevel.Series);
                        break;
                    case "IMAGE":
                        cFind = new DicomCFindRequest(DicomQueryRetrieveLevel.Image);
                        break;
                    case "PATIENT":
                        cFind = new DicomCFindRequest(DicomQueryRetrieveLevel.Patient);
                        break;
                    case "WORKLIST":
                    case "NA":
                        cFind = new DicomCFindRequest(DicomQueryRetrieveLevel.NotApplicable);
                        break;
                    case "STUDY":
                    default:
                        cFind = new DicomCFindRequest(DicomQueryRetrieveLevel.Study);
                        break;
                }

                _logger.Log(LogLevel.Information, $"{taskInfo} Request id: {routedItem.id} MessageID: {cFind.MessageID} attempt: {routedItem.attempts}");

                //default return tags

                // Encoding encoding = DicomEncoding.GetEncoding(cFindCharacterSet);
                // if(cFindCharacterSet != null){
                //     cFind.Dataset.AddOrUpdate(DicomTag.SpecificCharacterSet, cFindCharacterSet);                    
                // }
                // cFind.Dataset.AddOrUpdate(DicomTag.PatientID, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.PatientName, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.PatientBirthDate, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.PatientSex, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.StudyDate, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.AccessionNumber, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.StudyID, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedInstances, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.StudyDescription, encoding, "");
                // cFind.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, encoding, "");

                cFind.Dataset.AddOrUpdate(DicomTag.AccessionNumber, "");
                cFind.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedInstances, "");
                cFind.Dataset.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances, "");
                cFind.Dataset.AddOrUpdate(DicomTag.Modality, "");
                cFind.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, "");
                cFind.Dataset.AddOrUpdate(DicomTag.PatientID, "");
                cFind.Dataset.AddOrUpdate(DicomTag.PatientName, "");
                cFind.Dataset.AddOrUpdate(DicomTag.PatientBirthDate, "");
                cFind.Dataset.AddOrUpdate(DicomTag.PatientSex, "");
                cFind.Dataset.AddOrUpdate(DicomTag.StudyDate, "");
                cFind.Dataset.AddOrUpdate(DicomTag.StudyID, "");
                cFind.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, "");
                cFind.Dataset.AddOrUpdate(DicomTag.StudyDescription, "");
                // add the search tags
                foreach (KeyValuePair<string, string> tag in cFindParams.searchTags)
                {
                    try
                    {
                        // if(cFindCharacterSet != null){
                        //         Encoding iso = DicomEncoding.GetEncoding(cFindCharacterSet);
                        //         Encoding utf8 = Encoding.UTF8;
                        //         byte[] utfBytes = utf8.GetBytes(tag.Value);
                        //         byte[] isoBytes = Encoding.Convert(utf8, iso, utfBytes);
                        //         string value = iso.GetString(isoBytes);
                        //         //cFind.Dataset.AddOrUpdate(DicomTag.Parse(tag.Key), value);
                        //         cFind.Dataset.AddOrUpdate(DicomTag.Parse(tag.Key), Encoding.ASCII, value);

                        // } else {
                        cFind.Dataset.AddOrUpdate(DicomTag.Parse(tag.Key), tag.Value);

                        //                }
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, taskInfo);
                    }
                }

                //connect the cFind to the RoutedItem that originated the request
                //the cache was already primed in GetRequests
                cFind.UserState = routedItem;
                routedItem.MessageId = cFind.MessageID;
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();
                routedItem.status = RoutedItem.Status.PENDING;

                // var riToCache = (RoutedItem)routedItem.Clone();
                // riToCache.Enqueue(this, toRules, nameof(toRules));

                cFind.OnResponseReceived = (DicomCFindRequest request, DicomCFindResponse response) =>
                {
                    RoutedItem ri = (RoutedItem)request.UserState;

                    _logger.Log(LogLevel.Information, $"{taskInfo} Response id: {ri.id} MessageID: {request.MessageID} status: {response.Status} elapsed: {stopWatch.Elapsed}");

                    //DICOMConnection dicomConn = LITE.profile.GetDicomConnectionToLocalAETitle(Connection.localAETitle);
                    DICOMConnection dicomConn = _connectionFinder.GetDicomConnectionToLocalAETitle(_profileStorage.Current, Connection.localAETitle);

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

                    string key = "response";
                    Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>();
                    if (response.Dataset != null)
                    {
                        // Copy into a map, DicomDataset wont serialize
                        foreach (var dicomItem in response.Dataset)
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} Response id: {ri.id} MessageID: {request.MessageID}, {dicomItem.Tag.ToString()} {response.Dataset.GetValueOrDefault<string>(dicomItem.Tag, 0, "")}");
                            if (dicomItem.Tag.ToString() == "(0008,0054)") //BOUR-940 mask AETitle with conn name
                            {
                                returnTagData.Add(dicomItem.Tag.ToString(), Connection.name);

                            }
                            else
                            {
                                returnTagData.Add(dicomItem.Tag.ToString(), response.Dataset.GetValueOrDefault<string>(dicomItem.Tag, 0, ""));
                            }
                            if (dicomItem.Tag == DicomTag.StudyInstanceUID)
                            {
                                key = response.Dataset.GetValueOrDefault<string>(dicomItem.Tag, 0, "");
                            }
                        }
                        results.Add(key, returnTagData);
                    }

                    if ((results.Count == 0 && ri.response.Count == 0) || (results.Count > 0))
                    {
                        string jsonResults = JsonSerializer.Serialize(results);

                        // set results in RoutedItem
                        ri.response.Add(jsonResults);
                    }
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
                            toCache.toConnections.Clear(); //BOUR-863 the toConnections on the toCache object weren't being cleared before rules so it contained DICOMConnection which
                            toCache.attempts = 0;
                            toCache.lastAttempt = DateTime.MinValue;

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

                dicomClient.AddRequest(cFind);

            }
            catch (DicomDataException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} query: {routedItem} {e.Message} {e.StackTrace} ");

                routedItem.status = RoutedItem.Status.FAILED;
                routedItem.resultsTime = DateTime.Now;

                Dictionary<string, string> returnTagData = new Dictionary<string, string>
                {
                    { "StatusCode", "-1" },
                    { "StatusDescription", $"Error: {e.Message}" },
                    { "StatusErrorComment", $"Error: {e.StackTrace}" },
                    { "StatusState", "" }
                };

                string key = "response";

                Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>
                {
                    { key, returnTagData }
                };

                string jsonResults = JsonSerializer.Serialize(results);
                routedItem.response.Add(jsonResults);

                _routedItemManager.Init(routedItem);
                _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), true);
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();

                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);

                Dictionary<string, string> returnTagData = new Dictionary<string, string>
                {
                    { "StatusCode", "-1" },
                    { "StatusDescription", $"Error: {e.Message}" },
                    { "StatusErrorComment", $"Error: {e.StackTrace}" },
                    { "StatusState", "" }
                };

                string key = "response";

                Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>
                {
                    { key, returnTagData }
                };

                string jsonResults = JsonSerializer.Serialize(results);
                routedItem.response.Add(jsonResults);
                if (routedItem.attempts > Connection.maxAttempts)
                {
                    routedItem.status = RoutedItem.Status.FAILED;
                    routedItem.resultsTime = DateTime.Now;
                    _logger.Log(LogLevel.Debug, $"{taskInfo} id: {routedItem.id} exceeded max attempts.");

                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), true);
                }
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();

                _routedItemManager.Init(routedItem);
                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
            }
        }
    }
}
