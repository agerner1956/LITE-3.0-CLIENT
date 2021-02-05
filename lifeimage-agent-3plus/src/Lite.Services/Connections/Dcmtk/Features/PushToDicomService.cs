using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IPushToDicomService
    {
        Task PushtoDicom(int taskID, DcmtkConnection connection);
    }

    public sealed class PushToDicomService : DcmtkFeatureBase, IPushToDicomService
    {
        private readonly IDcmSendService _dcmSendService;
        private readonly IFindSCUService _findSCUService;
        private readonly IMoveSCUService _moveSCUService;
        private readonly IRoutedItemManager _routedItemManager;                

        public PushToDicomService(
            IDcmSendService dcmSendService,
            IFindSCUService findSCUService,
            IMoveSCUService moveSCUService,
            IRoutedItemManager routedItemManager,
            ILogger<PushToDicomService> logger) : base(logger)
        {
            _dcmSendService = dcmSendService;
            _findSCUService = findSCUService;
            _moveSCUService = moveSCUService;
            _routedItemManager = routedItemManager;
        }

        public override DcmtkConnection Connection { get; set; }

        public async Task PushtoDicom(int taskID, DcmtkConnection connection)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                var temp = Connection.toDicom.ToList();

                //move selected set for invocation
                foreach (var routedItem in temp)
                {
                    _routedItemManager.Init(routedItem);

                    RoutedItem newitem = null;
                    switch (routedItem.requestType)
                    {
                        case null:
                        case "cStore":
                            newitem = (RoutedItem)_routedItemManager.Clone();
                            _routedItemManager.Init(newitem);
                            _routedItemManager.Enqueue(Connection, Connection.toDcmsend, nameof(Connection.toDcmsend));

                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom));

                            break;

                        case "cFind":

                            newitem = _routedItemManager.Clone();

                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Enqueue(Connection, Connection.toFindSCU, nameof(Connection.toFindSCU));

                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom));

                            break;

                        case "cMove":

                            newitem = _routedItemManager.Clone();

                            _routedItemManager.Init(newitem);
                            _routedItemManager.Enqueue(Connection, Connection.toMoveSCU, nameof(Connection.toMoveSCU));

                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom));

                            break;
                    }
                }

                if (Connection.toDcmsend.Count > 0)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} Sending {Connection.toDcmsend.Count} items");
                    if (await _dcmSendService.DcmSend(taskID, Connection))
                    {
                        foreach (var routedItem in Connection.toDcmsend.ToList())
                        {
                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toDcmsend, nameof(Connection.toDcmsend));
                        }
                    }
                }

                if (Connection.toFindSCU.Count > 0)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} cFind {Connection.toFindSCU.Count} items");
                    foreach (var routedItem in Connection.toFindSCU.ToArray())
                    {
                        var result = _findSCUService.FindSCU(taskID, routedItem, Connection);
                        if (result.Status.Equals(RoutedItem.Status.COMPLETED))
                        {
                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toFindSCU, nameof(Connection.toFindSCU));
                        }
                    }
                }

                if (Connection.toMoveSCU.Count > 0)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} cMove {Connection.toMoveSCU.Count} items");
                    foreach (var routedItem in Connection.toMoveSCU.ToArray())
                    {
                        var result = await _moveSCUService.MoveSCU(taskID, routedItem, Connection);
                        if (result.status.Equals(RoutedItem.Status.COMPLETED))
                        {
                            _routedItemManager.Init(routedItem);
                            _routedItemManager.Dequeue(Connection, Connection.toMoveSCU, nameof(Connection.toMoveSCU));
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
            }
        }
    }
}
