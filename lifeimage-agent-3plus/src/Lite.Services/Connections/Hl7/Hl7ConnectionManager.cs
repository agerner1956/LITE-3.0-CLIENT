using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections.Hl7.Features;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Hl7
{
    public interface IHl7ConnectionManager : IConnectionManager
    {
    }

    public class Hl7ConnectionManager : ConnectionManager<HL7Connection>, IHl7ConnectionManager
    {
        [NonSerialized()]
        public List<BourneListens> listeners = new List<BourneListens>();

        [NonSerialized()]
        public ObservableCollection<BourneListens> deadListeners = new ObservableCollection<BourneListens>();

        [NonSerialized()]
        public List<TcpClient> clients = new List<TcpClient>();

        [NonSerialized()]
        public CancellationTokenSource cts = new CancellationTokenSource();

        [NonSerialized()]
        private readonly SemaphoreSlim ToHL7Signal = new SemaphoreSlim(0, 1);

        private readonly ISendToHl7Service _sendToHl7Service;
        private readonly IHl7ReaderService _hl7ReaderService;
        private readonly IHl7AcceptService _hl7AcceptService;
        private readonly IHl7StartService _hl7StartService;
        private readonly IHl7ClientsCleaner _hl7ClientsCleaner;

        public Hl7ConnectionManager(
            IHl7ReaderService hl7ReaderService,
            IHl7AcceptService hl7AcceptService,
            IHl7StartService hl7StartService,
            ISendToHl7Service sendToHl7Service,
            IHl7ClientsCleaner hl7ClientsCleaner,
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRoutedItemLoader routedItemLoader,
            IRulesManager rulesManager,
            IUtil util,
            ILITETask taskManager,
            ILogger<Hl7ConnectionManager> logger) :
            base(profileStorage, liteConfigService, routedItemManager, routedItemLoader, rulesManager, taskManager, logger, util)
        {
            _hl7StartService = hl7StartService;
            _hl7ReaderService = hl7ReaderService;
            _sendToHl7Service = sendToHl7Service;
            _hl7AcceptService = hl7AcceptService;
            _hl7ClientsCleaner = hl7ClientsCleaner;
        }

        protected override void ProcessImpl(Connection connection)
        {
            base.ProcessImpl(connection);

            Connection.ToHL7.CollectionChanged += ToHL7CollectionChanged;
        }

        protected virtual void ToHL7CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (ToHL7Signal.CurrentCount == 0) ToHL7Signal.Release();
                }
                catch (Exception) { }  //could be in the middle of being disposed and recreated
            }
        }

        public override void Init()
        {
            _logger.Log(LogLevel.Debug, $"{Connection.name} entering init.");

            base.Init();

            //register semaphore limits
            LITETask.Register($"{Connection.name}.read", Connection.maxInboundConnections);

            //2019-04-05 shb BOUR-934 old code was for conn.accept semaphore (without ip:port) which caused more than one accept per ip:port
            //in situations where multiple addresses were returned but say ipv6 was disabled.  Now we don't need to specify
            //so that when we create the task the type is specific to the ip and port which will register a semaphore of 1

            // var addressList = Dns.GetHostEntry(localHostname).AddressList;
            // foreach( var address in addressList){
            //     LITETask.Register($"{name}.accept: {address}:{localPort}", 1);
            // }

            //read the persisted RoutedItems bound for Rules

            string dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + Constants.Dirs.ToRules + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
            Directory.CreateDirectory(dir);
            var fileEntries = _util.DirSearch(dir, Constants.Extensions.MetaExt.ToSearchPattern());

            foreach (string file in fileEntries)
            {
                RoutedItem ri = _routedItemLoader.LoadFromFile(file);
                if (ri == null)
                {
                    continue;
                }

                ri.fromConnection = Connection.name;

                if (!toRules.Contains(ri))
                {
                    toRules.Add(ri);
                }
            }

            //read the persisted RoutedItems bound for HL7

            dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toHL7" + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
            Directory.CreateDirectory(dir);
            fileEntries = _util.DirSearch(dir, Constants.Extensions.MetaExt.ToSearchPattern());

            foreach (string file in fileEntries)
            {
                RoutedItem ri = _routedItemLoader.LoadFromFile(file);
                if (ri == null)
                {
                    continue;
                }

                ri.fromConnection = Connection.name;

                if (!Connection.ToHL7.Contains(ri))
                {
                    Connection.ToHL7.Add(ri);
                }
            }

            Connection.started = true;
        }

        public async Task Upload(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering Upload");

            try
            {
                do
                {
                    if (Connection.ToHL7.Count == 0)
                    {
                        bool success = await ToHL7Signal.WaitAsync(_profileStorage.Current.KickOffInterval, LITETask.cts.Token).ConfigureAwait(false);
                    }
                    await Task.Delay(1000).ConfigureAwait(false);  //to allow for some accumulation for efficient batching.

                    if (Connection.ToHL7.Count > 0)
                    {
                        await SendToHL7(taskID);
                    }
                } while (Connection.responsive);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, $"Wait Operation Canceled. Exiting Upload");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
                _logger.Log(LogLevel.Critical, $"{taskInfo} Exiting Upload");
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Upload");
            }
        }

        public override void Stop()
        {
            var taskInfo = $"connection: {Connection.name}";

            Connection.started = false;

            foreach (var bourne in listeners)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Stopping: {bourne.LocalEndpoint}");
                bourne.Stop();
                _logger.Log(LogLevel.Warning, $"{taskInfo} Stopped: {bourne.LocalEndpoint}");
                deadListeners.Add(bourne);
            }

            foreach (var bourne in deadListeners)
            {
                listeners.Remove(bourne);
            }
        }

        public async Task Start()
        {
            await _hl7StartService.Start(Connection, listeners, deadListeners, clients, Read);
        }

        public async Task Accept(BourneListens bourne, int taskID)
        {
            await _hl7AcceptService.Accept(Connection, bourne, clients, Read, taskID);
        }

        public async Task Read(TcpClient client, int taskID)
        {
            await _hl7ReaderService.Read(client, taskID, Connection);

            Clean();   // cleanup the connection, remove from list

            LITETask.Stop($"{Connection.name}.read");
        }

        public void Clean()
        {
            _hl7ClientsCleaner.Clean(Connection, clients);
        }

        public override async Task Kickoff(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                if (Connection.enabled == true && (Connection.inout == InOut.inbound || Connection.inout == InOut.both))
                {
                    await Start();  //starts the inbound connection if for some reason it is not started already.
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"{Connection.name} Cannot start listener: enabled: {Connection.enabled} inout: {Connection.inout}");
                }

                //connections
                _logger.Log(LogLevel.Debug, $"{Connection.name} Total Connections: {clients.Count}");
                _logger.Log(LogLevel.Debug, $"{Connection.name} Cleaning Connections...");

                Clean();

                _logger.Log(LogLevel.Debug, $"{Connection.name} Active Connections: {clients.Count}");

                if (LITETask.CanStart($"{Connection.name}.SendToRules"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await SendToRules(newTaskID, Connection.responsive)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.SendToRules", isLongRunning: true);
                }

                if (Connection.inout == InOut.both || Connection.inout == InOut.outbound)
                {
                    if (LITETask.CanStart($"{Connection.name}.Upload"))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await Upload(newTaskID)));
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.Upload", isLongRunning: false);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
                //throw e;
                throw;
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Kickoff");
            }
        }

        public async Task SendToHL7(int taskID)
        {
            await _sendToHl7Service.SendToHL7(taskID, Connection);
        }

        public override RoutedItem Route(RoutedItem routedItem, bool copy = false)
        {
            var taskInfo = $"task: {routedItem.TaskID} connection: {Connection.name}";

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Route item");

                if (routedItem.type == RoutedItem.Type.RPC)
                {

                    //populate the response cache and then send the response cache
                    routedItem = CacheResponse(routedItem);

                    if (routedItem.status == RoutedItem.Status.COMPLETED || routedItem.status == RoutedItem.Status.FAILED)
                    {
                        _logger.Log(LogLevel.Information, "Enqueuing item:", routedItem.name);
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(Connection, Connection.ToHL7, nameof(Connection.ToHL7));
                    }
                    else if (Connection.sendIntermediateResults)
                    {
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(Connection, Connection.ToHL7, nameof(Connection.ToHL7));
                    }
                }
                else
                {
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Enqueue(Connection, Connection.ToHL7, nameof(Connection.ToHL7), copy: copy);
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
                //throw e;
                throw;
            }

            return routedItem;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                deadListeners.Clear();
                ToHL7Signal.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
