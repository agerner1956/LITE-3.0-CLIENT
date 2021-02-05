using Lite.Core.Connections;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lite.Core.Models;
using Lite.Core.Interfaces;
using Lite.Services.Connections.Cloud.Features;
using Lite.Services.Connections.Cloud;
using Lite.Core;

namespace Lite.Services.Connections
{
    public interface ILifeImageCloudConnectionManager : IConnectionManager
    {
        Task<string> login(int taskID);
        SemaphoreSlim ToCloudSignal { get; }
    }

    public class LifeImageCloudConnectionManager : HttpConnectionManagerBase<LifeImageCloudConnection>, ILifeImageCloudConnectionManager
    {        
        public readonly SemaphoreSlim MarkDownloadsCompleteSignal = new SemaphoreSlim(0, 1);

        public readonly SemaphoreSlim MarkSeriesCompleteSignal = new SemaphoreSlim(0, 1);

        private readonly SemaphoreSlim _toCloudSignal = new SemaphoreSlim(0, 1);

        private readonly ICloudDownloadService _cloudDownloadService;
        private readonly ICloudUploadService _cloudUploadService;
        private readonly ICloudShareDestinationsService _shareDestinationsService;        
        private readonly ICloudLoginService _cloudLoginService;
        private readonly ICloudRegisterService _cloudRegisterService;
        private readonly ICloudLogoutService _cloudLogoutService;
        private readonly IMarkDownloadCompleteService _markDownloadCompleteService;
        private readonly ICloudConnectionCacheAccessor _cloudConnectionCacheAccessor;
        private readonly ICloudConnectionCacheManager _cloudConnectionCacheManager;
        private readonly ICloudKeepAliveService _keepAliveService;
        private readonly ICloudPingService _pingService;

        public LifeImageCloudConnectionManager(
            ICloudLoginService cloudLoginService,
            ICloudRegisterService cloudRegisterService,
            ICloudLogoutService cloudLogoutService,
            IRoutedItemManager routedItemManager,
            IRoutedItemLoader routedItemLoader,
            IRulesManager rulesManager,
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IMarkDownloadCompleteService markDownloadCompleteService,
            ILITETask taskManager,
            ILogger<LifeImageCloudConnectionManager> logger,
            IUtil util,
            ICloudDownloadService cloudDownloadService,
            ICloudUploadService cloudUploadService,
            ICloudShareDestinationsService shareDestinationsService,
            ICloudConnectionCacheAccessor cloudConnectionCacheAccessor,
            ICloudConnectionCacheManager cloudConnectionCacheManager,
            ICloudKeepAliveService keepAliveService,
            ICloudPingService cloudPingService)
            : base(profileStorage, liteConfigService, routedItemManager, routedItemLoader, rulesManager, taskManager, logger, util)
        {
            _cloudLoginService = cloudLoginService;
            _cloudRegisterService = cloudRegisterService;
            _cloudLogoutService = cloudLogoutService;
            _cloudDownloadService = cloudDownloadService;
            _cloudUploadService = cloudUploadService;
            _shareDestinationsService = shareDestinationsService;
            _markDownloadCompleteService = markDownloadCompleteService;
            _cloudConnectionCacheAccessor = cloudConnectionCacheAccessor;
            _cloudConnectionCacheManager = cloudConnectionCacheManager;
            _keepAliveService = keepAliveService;
            _pingService = cloudPingService;
        }

        public SemaphoreSlim ToCloudSignal => _toCloudSignal;

        protected override void ProcessImpl(Connection connection)
        {
            base.ProcessImpl(connection);

            Connection.toCloud.CollectionChanged += ToCloudCollectionChanged;
            Connection.markDownloadsComplete.CollectionChanged += MarkDownloadsCompleteCollectionChanged;
            Connection.markSeriesComplete.CollectionChanged += MarkSeriesCompleteCollectionChanged;
        }

        public override void Init()
        {
            if (Connection.Boxes.Count == 0)
            {
                //add sample box
                ShareDestinations shareDestinations = new ShareDestinations
                {
                    boxUuid = "19db62ba-128d-412c-8c4f-df28351e8ae0",
                    boxName = "LITESample"
                };
                Connection.Boxes.Add(shareDestinations);
            }

            List<string> fileEntries;

            string dir;
            lock (InitLock)
            {
                LITETask.Register($"{Connection.name}.Wado", Connection.maxWadoTasks);
                LITETask.Register($"{Connection.name}.Stow", Connection.maxStowTasks);
                LITETask.Register($"{Connection.name}.downloadStudy", Connection.maxStudyDownloadTasks);
                LITETask.Register($"{Connection.name}.putHL7", Connection.maxHL7UploadTasks);
                LITETask.Register($"{Connection.name}.PostResponse", Connection.maxPostResponseTasks);

                if (loginNeeded)
                {
                    base.Init();
                    int newTaskID = LITETask.NewTaskID();


                    // Register is a one-time operation.  Once you have your tenantID, don't call this again, but populate the tenantID upon class instantiation.
                    if (Connection.tenantID == null)
                    {
                        register(newTaskID).Wait();
                    }

                    // if you tried to register and you still don't have a tenantID, we're done.
                    if (Connection.tenantID == null)
                    {
                        _logger.Log(LogLevel.Warning, $"Account is not registered.");
                        return;
                    }

                    // //login
                    // newTaskID = LITETask.newTaskID();
                    // var result = login(newTaskID).Result;
                    // if (result != "OK")
                    // {
                    //     return;  //this is to prevent reading in work below if we don't login.
                    // }
                }

                //read the persisted RoutedItems bound for Cloud

                dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar +
                      "toCloud" + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
                Directory.CreateDirectory(dir);
                fileEntries = _util.DirSearch(dir, Constants.Extensions.MetaExt.ToSearchPattern());

                foreach (string file in fileEntries)
                {
                    var st = _routedItemLoader.LoadFromFile(file);
                    if (st == null)
                    {
                        continue;
                    }

                    st.fromConnection = Connection.name;

                    if (!Connection.toCloud.Contains(st))
                    {
                        Connection.toCloud.Add(st);
                    }
                }
            }

            //read the persisted RoutedItems bound for Rules

            dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toRules" +
                  Path.DirectorySeparatorChar + Constants.Dirs.Meta;
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

                if (!toRules.Contains(ri))
                {
                    toRules.Add(ri);
                }
            }

            Connection.started = true;
        }

        public override void Stop()
        {
            Connection.started = false;
            LiteHttpClient.Dispose();
        }

        public async Task<string> login(int taskID)
        {
            return await _cloudLoginService.login(taskID, Connection, this);
        }

        public async Task logout(int taskID)
        {
            await _cloudLogoutService.logout(taskID, Connection, this);
        }

        // Keep session alive to avoid timeouts etc
        public async Task KeepAlive()
        {
            await _keepAliveService.KeepAlive(Connection, GetHttpManager());
        }

        public async Task<bool> Ping()
        {
            return await _pingService.Ping(Connection, GetHttpManager());
        }
         
        protected virtual void ToCloudCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (ToCloudSignal.CurrentCount == 0) ToCloudSignal.Release();
                }
                catch (Exception)
                {
                } //could be in the middle of being disposed and recreated
            }
        }

        protected virtual void MarkDownloadsCompleteCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {

                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (MarkDownloadsCompleteSignal.CurrentCount == 0) MarkDownloadsCompleteSignal.Release();
                }
                catch (Exception)
                {
                } //could be in the middle of being disposed and recreated
            }
        }

        protected virtual void MarkSeriesCompleteCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    //                    MarkSeriesCompleteSignal.Release();
                }
                catch (Exception)
                {
                } //could be in the middle of being disposed and recreated
            }
        }

        public async Task Download(int taskID)
        {
            await _cloudDownloadService.Download(taskID, Connection, GetHttpManager());
        }

        public async Task Upload(int taskID)
        {
            await _cloudUploadService.Upload(taskID, Connection, this, this, GetHttpManager());
        }

        public override async Task Kickoff(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering Kickoff");

            try
            {
                if (Connection.isPrimary && LITETask.CanStart($"{Connection.name}.ExpireCache"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await ExpireCache(newTaskID)), LITETask.cts.Token);
                    LITETask.Start(newTaskID, task, $"{Connection.name}.ExpireCache", isLongRunning: true).Wait();
                }

                if (Connection.TestConnection)
                {
                    if (LITETask.CanStart($"{Connection.name}.PingCert"))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await PingCert(Connection.URL, newTaskID)));
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.PingCert", isLongRunning: false);
                    }
                }

                if (loginNeeded) await login(taskID);

                if (LITETask.CanStart($"{Connection.name}.KeepAlive"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await KeepAlive()));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.KeepAlive", isLongRunning: false);
                }

                if (LITETask.CanStart($"{Connection.name}.SendToRules"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await SendToRules(newTaskID, Connection.responsive)),
                        LITETask.cts.Token);
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.SendToRules", isLongRunning: true);
                }

                var currentProfile = _profileStorage.Current;
                var rules = currentProfile.rules;

                _rulesManager.Init(rules);

                if (_rulesManager.DoesRouteDestinationExistForSource(Connection.name))
                {
                    if (LITETask.CanStart($"{Connection.name}.Download"))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await Download(newTaskID)), LITETask.cts.Token);
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.Download", isLongRunning: false);
                    }

                    if (LITETask.CanStart($"{Connection.name}.markDownloadComplete"))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await markDownloadComplete(taskID)), LITETask.cts.Token);
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.markDownloadComplete", isLongRunning: false);
                    }
                }

                if (LITETask.CanStart($"{Connection.name}.Upload"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await Upload(newTaskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.Upload", isLongRunning: false);
                }


                if (LITETask.CanStart($"{Connection.name}.getShareDestinations"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await getShareDestinations(newTaskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.getShareDestinations", isLongRunning: false);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} Exiting Kickoff Due to Exception");
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Kickoff");
            }
        }

        public override RoutedItem Route(RoutedItem routedItem, bool copy = false)
        {
            var taskInfo = $"task: {routedItem.TaskID} connection: {Connection.name} type: {routedItem.type} id: {routedItem.id}";

            try
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo}");

                if (routedItem.type == RoutedItem.Type.DICOM && routedItem.sourceFileName == null)
                {
                    _logger.Log(LogLevel.Critical,
                        $"{taskInfo} RoutedItem.Type.DICOM requires routedItem.sourceFileName, will not route this request");
                    return null;
                }

                if (routedItem.type != RoutedItem.Type.COMPLETION)
                {
                    //populate the response cache
                    routedItem = CacheResponse(routedItem);
                }

                if (routedItem.type == RoutedItem.Type.RPC || routedItem.type == RoutedItem.Type.COMPLETION)
                {
                    if (routedItem.status == RoutedItem.Status.COMPLETED ||
                        routedItem.status == RoutedItem.Status.FAILED)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Enqueuing Completed response");
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(this.Connection, Connection.toCloud, nameof(Connection.toCloud));
                    }
                    else if (Connection.sendIntermediateResults)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Enqueuing Intermediate response");
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(this.Connection, Connection.toCloud, nameof(Connection.toCloud));
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Enqueuing");

                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Enqueue(this.Connection, Connection.toCloud, nameof(Connection.toCloud), copy: copy);
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

        // markDownloadComplete is used to remove an item that was in the /studies call
        public async Task markDownloadComplete(int taskID)
        {
            await _markDownloadCompleteService.markDownloadComplete(taskID, Connection, GetHttpManager());
        }

        public Task getShareDestinations(int taskID)
        {
            return _shareDestinationsService.GetShareDestinations(taskID, Connection, GetHttpManager());
        }

        // register to get a tenantID, can only be done one time
        public async Task register(int taskID)
        {
            await _cloudRegisterService.register(taskID, Connection, GetHttpManager());
        }

        public Task<RegisterAsAdmin> register(string username, string password, string org = null, string serviceName = null)
        {
            return _cloudRegisterService.register(Connection, username, password, org, serviceName);
        }

        public async Task ExpireCache(int taskID)
        {
            await _cloudConnectionCacheManager.ExpireCache(Connection, this, taskID);
        }

        public string GetCachedItemMetaData(RoutedItem routedItem, long taskID)
        {
            return _cloudConnectionCacheAccessor.GetCachedItemMetaData(Connection, routedItem, taskID);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                ToCloudSignal.Dispose();
                MarkDownloadsCompleteSignal.Dispose();
                MarkSeriesCompleteSignal.Dispose();
            }
        }
    }
}
