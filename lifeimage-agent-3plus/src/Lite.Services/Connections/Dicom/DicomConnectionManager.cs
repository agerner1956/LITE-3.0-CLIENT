using Dicom;
using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lite.Services.Connections.Dicom.Features;
using Lite.Core;

namespace Lite.Services.Connections.Dicom
{
    public interface IDicomConnectionManager : IConnectionManager
    {
    }

    public class DicomConnectionManager : ConnectionManager<DICOMConnection>, IDicomConnectionManager
    {
        public DicomClient dicomClient;

        public string cFindCharacterSet = DicomEncoding.Default.EncodingName;

        /// <summary>
        ///  List of dicom operations the connection wil handle. Values should be cFind, cMove or cStore
        /// </summary>
        public List<string> dicomOperations { get; set; }

        //[NonSerialized()]
        //public ObservableCollection<RoutedItem> toDicom = new ObservableCollection<RoutedItem>();

        [NonSerialized()]
        public static List<IDicomServer> dicomListeners = new List<IDicomServer>();

        [NonSerialized()]
        private readonly SemaphoreSlim toDicomSignal = new SemaphoreSlim(0, 1);

        public List<string> AcceptedImageTransferSyntaxUIDs { get; set; } = new List<string>();
        public List<string> PossibleImageTransferSyntaxUIDs { get; set; } = new List<string>();

        private readonly IDicomUtil _dicomUtil;
        private readonly ISendToDicomService _sendToDicomService;
        private IDicomServerFactory _dicomServerFactory;

        public DicomConnectionManager(
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRoutedItemLoader routedItemLoader,
            IRulesManager rulesManager,
            ISendToDicomService sendToDicomService,
            IDicomUtil dicomUtil,
            IUtil util,
            ILITETask taskManager,
            ILogger<DicomConnectionManager> logger)
            : base(profileStorage, liteConfigService, routedItemManager, routedItemLoader, rulesManager, taskManager, logger, util)
        {
            _dicomUtil = dicomUtil;
            _sendToDicomService = sendToDicomService;
        }

        protected override void ProcessImpl(Connection connection)
        {
            base.ProcessImpl(connection);
            Connection.toDicom.CollectionChanged += ToDicomChanged;
            _dicomServerFactory = new DicomServerFactory(Connection, _logger);
        }

        public override void Init()
        {
            if (dicomOperations == null || dicomOperations.Count == 0)
            {
                dicomOperations = new List<string> { "cFind", "cMove", "cStore" };
            }

            dicomClient = new DicomClient();
            dicomClient.AssociationAccepted += OnAssociationAccept;
            dicomClient.AssociationRejected += OnAssociationReject;
            dicomClient.AssociationReleased += OnAssociationRelease;

            SetupDicomServiceOptions();

            //read the persisted RoutedItems bound for Dicom

            var currentProfile = _profileStorage.Current;

            var dir = currentProfile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toDicom" + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
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

                if (!Connection.toDicom.Contains(ri))
                {
                    Connection.toDicom.Add(ri);
                }
            }

            //read the persisted RoutedItems bound for Rules

            dir = _profileStorage.Current.tempPath +
                Path.DirectorySeparatorChar +
                Connection.name +
                Path.DirectorySeparatorChar +
                Constants.Dirs.ToRules +
                Path.DirectorySeparatorChar +
                Constants.Dirs.Meta;

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

                if (!Connection.toRules.Contains(ri))
                {
                    Connection.toRules.Add(ri);
                }
            }

            Connection.started = true;
        }

        public void SetupDicomServiceOptions()
        {
            dicomClient.Linger = Connection.Linger;

            //todo: send DICOM logger. WHY???
            //dicomClient.Logger = Logger.logger;
            dicomClient.NegotiateAsyncOps(Connection.asyncInvoked, Connection.asyncPerformed);

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

            dicomClient.Options = dsoptions;
        }

        public override void Stop()
        {
            _logger.Log(LogLevel.Information, $"Stopping {Connection.name}");

            Connection.started = false;

            try
            {
                if (dicomClient != null)
                {
                    dicomClient.ReleaseAsync();
                    dicomClient.AbortAsync();
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }

            try
            {
                foreach (var server in dicomListeners)
                {
                    server.Stop();
                    server.Dispose();
                }
                dicomListeners.Clear();
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }

        public void CreateListener()
        {
            var taskStatus = $"{Connection.name}:";

            var dicomServer = _dicomServerFactory.CreateListener(dicomListeners);
            if (dicomServer != null)
            {
                // already exist
                return;
            }

            dicomListeners.Add(dicomServer);

            _logger.Log(LogLevel.Information, $"{taskStatus} listening on port {Connection.localPort}");
            _logger.Log(LogLevel.Information, $"{taskStatus} Verify with Linux/Mac: lsof -n -i:{Connection.localPort} | grep LISTEN");
            _logger.Log(LogLevel.Information, $"{taskStatus} Verify with Windows: netstat -abno (requires elevated privileges)");
        }

        public IDicomServer GetDicomServer(int port)
        {
            return dicomListeners.Find(a => a.Port == port);
        }

        protected virtual void ToDicomChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (toDicomSignal.CurrentCount == 0) toDicomSignal.Release();
                }
                catch (Exception) { }  //could be in the middle of being disposed and recreated
            }
        }

        public void OnAssociationAccept(object sender, AssociationAcceptedEventArgs args)
        {
            _logger.Log(LogLevel.Information, $"OnAssociationAccept {args.Association.CalledAE}");
        }

        public void OnAssociationReject(object sender, AssociationRejectedEventArgs args)
        {
            _logger.Log(LogLevel.Information, $"OnAssociationReject {args.Reason} {args.Result} {args.Source}");
        }
        public void OnAssociationRelease(object sender, EventArgs args)
        {
            _logger.Log(LogLevel.Information, $"OnAssociationRelease {sender.ToString()} {args.ToString()}");
        }

        public override async Task Kickoff(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            try
            {
                if (Connection.enabled == true && Connection.localPort != 0)
                {
                    CreateListener();
                }

                if (dicomClient == null)
                {
                    Init();
                }

                _logger.Log(LogLevel.Information, $"{taskInfo} toDicom: {Connection.toDicom.Count} toRules: {Connection.toRules.Count}");

                if (LITETask.CanStart($"{Connection.name}.SendToDicom"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await SendToDicom(taskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.SendToDicom", isLongRunning: false);
                }

                if (LITETask.CanStart($"{Connection.name}.SendToRules"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await SendToRules(newTaskID, Connection.responsive)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.SendToRules", isLongRunning: true);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, $"Wait Operation Canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Kickoff");
            }
        }

        public async Task SendToDicom(int taskID)
        {
            await _sendToDicomService.SendToDicom(taskID, Connection, dicomClient, toDicomSignal);
        }

        public override RoutedItem Route(RoutedItem routedItem, bool copy = false)
        {
            try
            {
                var taskInfo = $"task: {routedItem.TaskID} connection: {Connection.name}:";

                //check if dicom, if not dicomize since dicom only does dicom, duh.
                if (routedItem.sourceFileName != null)
                {
                    if (!_dicomUtil.IsDICOM(routedItem))
                    {
                        routedItem = _dicomUtil.Dicomize(routedItem);
                    }
                }
                //enqueue the routedItem

                _routedItemManager.Init(routedItem);
                _routedItemManager.Enqueue(Connection, Connection.toDicom, nameof(Connection.toDicom), copy: copy);

            }
            catch (Exception e)
            {
                _logger.LogFullException(e);

                //throw e;
                throw;
            }

            return routedItem;
        }

        public bool CanPerformOperation(string operation)
        {
            return (dicomOperations == null || dicomOperations.Count == 0 || (dicomOperations.Find(a => a.ToLower() == operation.ToLower()) != null));
        }

        protected override void Dispose(bool disposing)
        {
            toDicomSignal.Dispose();

            if (dicomClient != null)
            {
                dicomClient.Abort();
            }

            base.Dispose(disposing);
        }


#if (DEBUG)
        // Keep around of a bit until things shake out
        public async static void Test()
        {
            var client = new DicomClient();
            client.AddRequest(new DicomCStoreRequest(@"D:\\dvl\\lifeImage\\Images\\Generated\\Studies\\200mb\\SERIES_1\\INS_1"));
            client.AddRequest(new DicomCStoreRequest(@"D:\\dvl\\lifeImage\\Images\\Generated\\Studies\\200mb\\SERIES_1\\INS_2"));
            //            await client.SendAsync("127.0.0.1", 12345, false, "SCU", "ANY-SCP");

            await client.SendAsync("localhost", 11112, false, "LITEDCMRECV", "DCMRECV");
            client.Release();

            client = new DicomClient();
            client.AddRequest(new DicomCStoreRequest(@"D:\\dvl\\lifeImage\\Images\\Generated\\Studies\\200mb\\SERIES_1\\INS_3"));
            client.AddRequest(new DicomCStoreRequest(@"D:\\dvl\\lifeImage\\Images\\Generated\\Studies\\200mb\\SERIES_1\\INS_4"));
            //            await client.SendAsync("127.0.0.1", 12345, false, "SCU", "ANY-SCP");

            await client.SendAsync("localhost", 11112, false, "LITEDCMRECV", "DCMRECV");
        }
#endif
    }
}
