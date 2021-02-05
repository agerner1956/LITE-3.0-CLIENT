using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface ISendToDicomService
    {
        Task SendToDicom(int taskID, DICOMConnection connection, DicomClient dicomClient, SemaphoreSlim toDicomSignal);
    }

    public sealed class SendToDicomService : ISendToDicomService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;        
        private readonly IDicomCEchoCommand _dicomCEchoCommand;
        private readonly IDicomCFindCommand _dicomCFindCommand;
        private readonly IDicomCMoveCommand _dicomCMoveCommand;
        private readonly IDicomCStoreCommand _dicomCStoreCommand;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public SendToDicomService(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,            
            IDicomCEchoCommand dicomCEchoCommand,
            IDicomCFindCommand dicomCFindCommand,
            IDicomCMoveCommand dicomCMoveCommand,
            IDicomCStoreCommand dicomCStoreCommand,
            ILITETask taskManager,
            ILogger<SendToDicomService> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;            
            _dicomCEchoCommand = dicomCEchoCommand;
            _dicomCFindCommand = dicomCFindCommand;
            _dicomCMoveCommand = dicomCMoveCommand;
            _dicomCStoreCommand = dicomCStoreCommand;
            _taskManager = taskManager;
            _logger = logger;
        }

        public DICOMConnection Connection { get; set; }
        public async Task SendToDicom(int taskID, DICOMConnection connection, DicomClient dicomClient, SemaphoreSlim toDicomSignal)
        {
            Connection = connection;
            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            try
            {
                do
                {
                    if (Connection.TestConnection)
                    {
                        await CEcho(taskID, dicomClient);
                    }

                    bool success = await toDicomSignal.WaitAsync(_profileStorage.Current.KickOffInterval, _taskManager.cts.Token).ConfigureAwait(false);

                    while (Connection.toDicom.ToList().FindAll(e => e.lastAttempt < DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)).Count > 0)
                    {
                        // batch up items along maxRequestsPerAssociation and if the attempt > 1 break out and send it. 
                        List<List<RoutedItem>> batches = GetBatches(taskInfo);

                        _logger.Log(LogLevel.Debug, $"{taskInfo} batches to send: {batches.Count}");

                        //queue up the requests for each association
                        foreach (var association in batches)
                        {
                            await ProcessBatches(association, dicomClient, taskID, taskInfo, stopWatch);
                        }
                        await Task.Delay(+_profileStorage.Current.backlogInterval).ConfigureAwait(false);
                        stopWatch.Restart();
                    }

                    // toDicomSignal.Dispose();
                    // toDicomSignal = new SemaphoreSlim(0, 1);

                } while (Connection.responsive);
            }
            catch (DicomAssociationRejectedException e)
            {
                _logger.LogFullException(e, $"{taskInfo} Dicom Association Failed");                
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.SendToDicom");
            }
        }

        private List<List<RoutedItem>> GetBatches(string taskInfo)
        {
            //batch up items along maxRequestsPerAssociation and if the attempt > 1 break out and send it. 

            List<List<RoutedItem>> batches = new List<List<RoutedItem>>();
            List<RoutedItem> batch = new List<RoutedItem>();
            batches.Add(batch);

            int requests = 0;
            foreach (var ri in Connection.toDicom.ToArray())
            {
                if (ri.lastAttempt < DateTime.Now.AddMinutes(-Connection.retryDelayMinutes)) //not attempted lately
                {
                    ri.attempts++;
                    ri.lastAttempt = DateTime.Now;

                    if (ri.attempts > Connection.maxAttempts)
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} {ri.sourceFileName} exceeded maxAttempts");

                        Dictionary<string, string> returnTagData = new Dictionary<string, string>
                                    {
                                        { "StatusCode", "-1" },
                                        { "StatusDescription", $"Error: id: {ri.id} name: {Connection.name} remoteAETitle: {Connection.remoteAETitle} exceeded max attempts." },
                                        { "StatusErrorComment", "" },
                                        { "StatusState", "" }
                                    };

                        string key = "response";

                        Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>
                        {
                            { key, returnTagData }
                        };

                        string jsonResults = JsonConvert.SerializeObject(results);
                        ri.response.Add(jsonResults);

                        ri.fromConnection = Connection.name;
                        ri.status = RoutedItem.Status.FAILED;
                        ri.resultsTime = DateTime.Now;
                        ri.toConnections.Clear();

                        _routedItemManager.Init(ri);
                        _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), error: true);
                        if (ri.type == RoutedItem.Type.RPC)
                        {  //this is a round-trip request versus one-way
                            _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
                        }
                        continue;
                    }

                    batch.Add(ri);
                    requests++;

                    if (requests >= Connection.maxRequestsPerAssociation || ri.attempts > 1)
                    {  //start a new batch

                        _logger.Log(LogLevel.Debug, $"{taskInfo} batch count: {batch.Count}");

                        batch = new List<RoutedItem>();
                        batches.Add(batch);
                        requests = 0;
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} will retry id: {ri.id} attempts: {ri.attempts} meta: {ri.RoutedItemMetaFile} at {ri.lastAttempt.AddMinutes(Connection.retryDelayMinutes)}");
                }
            }

            return batches;
        }

        private async Task ProcessBatches(List<RoutedItem> association, DicomClient dicomClient, int taskID, string taskInfo, Stopwatch stopWatch)
        {
            //queue up the requests for this association
            foreach (var ri in association)
            {
                switch (ri.requestType)
                {
                    case null:
                    case "cStore":
                        CStore(ri, taskID, dicomClient);
                        break;

                    case "cFind":
                        CFind(ri, taskID, dicomClient);
                        break;

                    case "cMove":
                        CMove(ri, taskID, dicomClient);
                        break;
                }
            }

            if (dicomClient.IsSendRequired && dicomClient.CanSend)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} calling SendAsync...");

                DicomSendRequestService dicomSendRequest = new DicomSendRequestService(dicomClient, Connection, _logger);

                try
                {
                    await dicomSendRequest.SendRequest(taskInfo, stopWatch);

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
                    _logger.LogFullException(e, $"{taskInfo} SendAsync: ");
                }
            }
        }

        public async Task CEcho(int taskID, DicomClient dicomClient)
        {
            _dicomCEchoCommand.InitClient(dicomClient);
            await _dicomCEchoCommand.CEcho(Connection, taskID);
        }

        public void CFind(RoutedItem routedItem, int taskID, DicomClient dicomClient)
        {
            _dicomCFindCommand.InitClient(dicomClient);
            _dicomCFindCommand.CFind(routedItem, Connection, taskID);
        }

        public void CMove(RoutedItem routedItem, long taskID, DicomClient dicomClient)
        {
            _dicomCMoveCommand.InitClient(dicomClient);
            _dicomCMoveCommand.CMove(routedItem, taskID, Connection);
        }

        /// <summary>
        /// Push a file to DICOM.
        /// </summary>
        /// <param name="routedItem"></param>
        /// <param name="taskID"></param>
        /// <param name="dicomClient"></param>
        public void CStore(RoutedItem routedItem, long taskID, DicomClient dicomClient)
        {
            _dicomCStoreCommand.InitClient(dicomClient);
            _dicomCStoreCommand.CStore(routedItem, taskID, Connection);
        }
    }
}
