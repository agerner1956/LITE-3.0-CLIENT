using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections.Lite.Features;
using Lite.Services.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public interface ILiteConnectionManager : IConnectionManager
    {

    }

    public class LiteConnectionManager : HttpConnectionManagerBase<LITEConnection>, ILiteConnectionManager
    {        
        public static readonly ObservableCollection<Profile> LITERegistry = new ObservableCollection<Profile>();
        
        public ObservableCollection<string[]> markDownloadsComplete = new ObservableCollection<string[]>();
        
        public ObservableCollection<Series> markSeriesComplete = new ObservableCollection<Series>();
        
        public readonly SemaphoreSlim ToEGSSignal = new SemaphoreSlim(0, 1);
        
        public readonly SemaphoreSlim FromEGSSignal = new SemaphoreSlim(0, 1);
        
        public Dictionary<string, HubConnection> hubConnections = new Dictionary<string, Microsoft.AspNetCore.SignalR.Client.HubConnection>();
        
        public ObservableCollection<RoutedItem> hubMessages = new ObservableCollection<RoutedItem>();
        
        private readonly SemaphoreSlim HubMessagesSignal = new SemaphoreSlim(0, 1);

        private readonly ILiteDownloadService _liteDownloadService;
        private readonly ILiteUploadService _liteUploadService;
        private readonly IRegisterWithEGSService _registerWithEGSService;
        private readonly ILiteConnectionPurgeService _purgeService;
        private readonly IGetLiteReresourcesService _getLiteReresourcesService;
        private readonly ILitePingService _litePingService;
        private readonly ISendToAllHubsService _sendToAllHubsService;
        private readonly IConnectionFinder _connectionFinder;

        public LiteConnectionManager(
            ILiteDownloadService liteDownloadService,
            IGetLiteReresourcesService getLiteReresourcesService,
            IRegisterWithEGSService registerWithEGSService,
            ILiteConnectionPurgeService purgeService,
            ILiteUploadService liteUploadService,
            ILitePingService litePingService,
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRoutedItemLoader routedItemLoader,
            IRulesManager rulesManager,
            ISendToAllHubsService sendToAllHubsService,
            IConnectionFinder connectionFinder,
            ILITETask taskManager,
            ILogger<LiteConnectionManager> logger,
            IUtil util)
            : base(profileStorage, liteConfigService, routedItemManager, routedItemLoader, rulesManager, taskManager, logger, util)
        {
            _liteUploadService = liteUploadService;
            _liteDownloadService = liteDownloadService;
            _purgeService = purgeService;
            _registerWithEGSService = registerWithEGSService;
            _getLiteReresourcesService = getLiteReresourcesService;
            _litePingService = litePingService;
            _sendToAllHubsService = sendToAllHubsService;
            _connectionFinder = connectionFinder;

            hubMessages.CollectionChanged += HubMessagesCollectionChanged;
        }

        protected override void ProcessImpl(Connection connection)
        {
            base.ProcessImpl(connection);

            Connection.toEGS.CollectionChanged += ToEGSCollectionChanged;
            Connection.fromEGS.CollectionChanged += FromEGSCollectionChanged;
            //            toRules.CollectionChanged += ToRulesCollectionChanged;
        }

        protected virtual void ToEGSCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (ToEGSSignal.CurrentCount == 0) ToEGSSignal.Release();
                }
                catch (Exception) { }  //could be in the middle of being disposed and recreated
            }
        }

        protected virtual void FromEGSCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (FromEGSSignal.CurrentCount == 0) FromEGSSignal.Release();
                }
                catch (Exception) { }  //could be in the middle of being disposed and recreated
            }
        }

        protected virtual void HubMessagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (HubMessagesSignal.CurrentCount == 0) HubMessagesSignal.Release();
                }
                catch (Exception) { }  //could be in the middle of being disposed and recreated
            }
        }

        public override void Init()
        {
            LITETask.Register($"{Connection.name}.DownloadViaHttp", Connection.maxDownloadViaHttpTasks);
            LITETask.Register($"{Connection.name}.Store", Connection.maxStoreTasks);
            List<string> fileEntries;

            var profile = _profileStorage.Current;

            lock (InitLock)
            {
                base.Init();

                //read the persisted RoutedItems bound for EGS

                string dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toEGS" + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
                Directory.CreateDirectory(dir);
                fileEntries = _util.DirSearch(dir, Constants.Extensions.MetaExt.ToSearchPattern());

                foreach (string file in fileEntries)
                {
                    RoutedItem st = _routedItemLoader.LoadFromFile(file);
                    if (st == null)
                    {
                        continue;
                    }

                    st.fromConnection = Connection.name;

                    if (!Connection.toEGS.Contains(st))
                    {
                        Connection.toEGS.Add(st);
                    }
                }

                //read the persisted RoutedItems bound for Rules

                dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toRules" + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
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

                //read the persisted RoutedItems inbound from EGS
                dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "fromEGS" + Path.DirectorySeparatorChar + Constants.Dirs.Meta;
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

                    if (!Connection.fromEGS.Contains(ri))
                    {
                        Connection.fromEGS.Add(ri);
                    }
                }
            }

            Connection.started = true;
        }

        //public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        //{

        //    return WebHost.CreateDefaultBuilder(args).UseStartup<Startup>();
        //    // return BlazorWebAssemblyHost.CreateDefaultBuilder()
        //    //     .UseBlazorStartup<Startup>();

        //}

        public override async Task Kickoff(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering Kickoff");

            try
            {
                var profile = _profileStorage.Current;

                LiteProfileUtils liteProfileUtils = new LiteProfileUtils(profile);

                Connection.boxes = liteProfileUtils.GetBoxes();

                Connection.shareDestinations = liteProfileUtils.GetShareDestinations();

                if (Connection.inout == InOut.inbound | Connection.inout == InOut.both)
                {
                    if (Connection.egs)
                    {
                        if ((LiteEngine.kickOffCount + 9) % 10 == 0)
                        {
                            if (LITETask.CanStart($"{Connection.name}.Purge"))
                            {
                                var newTaskID = LITETask.NewTaskID();
                                Task task = new Task(new Action(async () => await Purge(newTaskID)), LITETask.cts.Token);
                                await LITETask.Start(newTaskID, task, $"{Connection.name}.Purge", isLongRunning: false);
                            }
                        }

                        //EGS http Listening port
                        if (LITETask.CanStart($"{Connection.name}.EGS"))
                        {
                            string[] args = { $"name={Connection.name}" };
                            var newTaskID = LITETask.NewTaskID();

                            // todo: migrate later
//                            Task task = new Task(new Action(async () => await CreateWebHostBuilder(args).Build().RunAsync()), LITETask.cts.Token);

//#pragma warning disable 4014

//                            //task.ContinueWith(x => LITETask.Stop($"{name}.EGS"), ct);

//#pragma warning restore 4014

//                            await LITETask.Start(newTaskID, task, $"{Connection.name}.EGS", $"{Connection.name}.EGS", isLongRunning: true);
//                            var stopWatch = new Stopwatch();
//                            stopWatch.Start();

//                            do
//                            {
//                                await Task.Delay(10000).ConfigureAwait(false); //loop until server is started

//                            } while (!await ping());
//                            stopWatch.Stop();
//                            _logger.Log(LogLevel.Information, $"{taskInfo} EGS Server Started elapsed: {stopWatch.Elapsed}");
                            OpenBrowser();
                        }
                    }

                    await ConnectToHubs();
                    await RegisterWithEGS(taskID);

                    if (LITETask.CanStart($"{Connection.name}.Download"))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await Download(newTaskID)), LITETask.cts.Token);
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.Download", isLongRunning: false);
                    }
                }

                if (Connection.inout == InOut.outbound | Connection.inout == InOut.both)
                {
                    if (Connection.TestConnection)
                    {
                        if (LITETask.CanStart($"{Connection.name}.PingCert"))
                        {
                            var newTaskID = LITETask.NewTaskID();
                            Task task = new Task(new Action(async () => await PingCert(Connection.URL, newTaskID)));
                            await LITETask.Start(newTaskID, task, $"{Connection.name}.PingCert", isLongRunning: false);
                        }
                    }

                    if (LITETask.CanStart($"{Connection.name}.Upload"))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await Upload(newTaskID)));
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.Upload", isLongRunning: false);
                    }
                }

                if (LITETask.CanStart($"{Connection.name}.SendToRules"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await SendToRules(newTaskID, Connection.responsive)), LITETask.cts.Token);
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.SendToRules", isLongRunning: true);
                }

                if (LITETask.CanStart($"{Connection.name}.ProcessHubMessages"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await ProcessHubMessages(newTaskID)), LITETask.cts.Token);
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.ProcessHubMessages", isLongRunning: false);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} Exiting Kickoff Due to Exception ");                
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Kickoff");
            }
        }

        public async Task Upload(int taskID)
        {
            _sendToAllHubsService.Init(hubConnections);
            await _liteUploadService.Upload(taskID, Connection, ToEGSSignal, _sendToAllHubsService);
        }

        private async Task Purge(int taskID)
        {
            var taskInfo = $"task: {taskID}";            

            try
            {
                await Task.Run(() =>
                {
                    _purgeService.Purge(taskID, Connection);
                });
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Purge");
            }
        }

        public async Task ProcessHubMessages(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var profile = _profileStorage.Current;

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering ProcessHubMessages");

            try
            {
                do
                {
                    bool success = await HubMessagesSignal.WaitAsync(profile.KickOffInterval, LITETask.cts.Token).ConfigureAwait(false);
                    // HubMessagesSignal.Dispose();
                    // HubMessagesSignal = new SemaphoreSlim(0, 1);

                } while (Connection.responsive);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, $"Wait Operation Canceled. Exiting ProcessHubMessages");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);   
                _logger.Log(LogLevel.Critical, $"{taskInfo} Exiting ProcessHubMessages");
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.ProcessHubMessages");
            }
        }

        public async Task Download(int taskID)
        {
            await _liteDownloadService.Download(taskID, Connection, GetHttpManager(), FromEGSSignal);
        }

        [Obsolete]
        public async Task<int> GetResources(int taskID)
        {
            return await _getLiteReresourcesService.GetResources(taskID, Connection, this);
        }

        public override void Stop()
        {
            Connection.started = false;
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
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(Connection, Connection.toEGS, nameof(Connection.toEGS));
                    }
                    else if (Connection.sendIntermediateResults)
                    {
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Enqueue(Connection, Connection.toEGS, nameof(Connection.toEGS));
                    }
                }
                else
                {
                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Enqueue(Connection, Connection.toEGS, nameof(Connection.toEGS), copy: copy);
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

        public async Task RegisterWithEGS(int taskID)
        {
            await _registerWithEGSService.RegisterWithEGS(taskID, Connection, GetHttpManager());
        }

        public async Task GetEGSShares()
        {
            try
            {
                //look up known LITEs each EGS knows about
                IPHostEntry hostEntry = Dns.GetHostEntry(Connection.remoteHostname);
                foreach (var iPAddress in hostEntry.AddressList)
                {
                    //contact each EGS and get the location of known shares

                    //perhaps use the signalr chat hub
                    await Task.Yield();
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

        public async Task RegisterLITE(Profile lite)
        {
            try
            {                
                //server side method the LITE will call to register itself, invoked from LITEController
                var registered = LITERegistry.ToList().FindAll(e => _connectionFinder.GetPrimaryLifeImageConnection(e).tenantID ==_connectionFinder.GetPrimaryLifeImageConnection(lite).tenantID);
                switch (registered.Count)
                {
                    case 0:
                        LITERegistry.Add(lite);
                        break;
                    case 1:
                        //already registered, just update
                        registered[0] = lite;
                        break;
                    default:
                        //duplicate entries?
                        break;
                }

                await Task.Yield();

            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }

            return;
        }

        public async Task<HubConnection> StartHubConnection(string url)
        {
            HubConnection hubConnection = null;

            try
            {
                hubConnection = new HubConnectionBuilder()

                    // todo: migrate WithUrl later
                #region to migrate
                    //    .WithUrl(url, configure =>
                    //{
                    //    configure.HttpMessageHandlerFactory = options =>
                    //    new HttpClientHandler { ServerCertificateCustomValidationCallback = RemoteCertificateValidationCallback };

                    //    configure.WebSocketConfiguration = options =>
                    //    { options.RemoteCertificateValidationCallback = RemoteCertificateValidationCallback; };
                    //})
                #endregion
                    .Build();
                // .AddMessagePackProtocol(options =>
                //                      {
                //                          options.FormatterResolvers = new List<MessagePack.IFormatterResolver>()
                //                         {
                //                         MessagePack.Resolvers.StandardResolver.Instance
                //                         };
                //                      })

                hubConnection.Closed += async (error) =>
                {
                    await Task.Delay(new Random().Next(0, 5) * 1000).ConfigureAwait(false);
                    await hubConnection.StartAsync();
                };

                hubConnection.On<string, string>("ReceiveMessage", (user, message) =>
                {
                    List<string[]> msg = null;

                    try
                    {
                        using (var stream = message.StreamFromString())
                        {
                            var serializer = new DataContractJsonSerializer(typeof(List<string[]>));
                            msg = serializer.ReadObject(stream) as List<string[]>;
                        }

                        var newMessage = $"{user}: {message}";
                        foreach (var msgPart in msg)
                        {
                            //the message is for this LITE to download something
                            RoutedItem ri = new RoutedItem
                            {
                                type = RoutedItem.Type.FILE,
                                fromConnection = Connection.name,
                                box = msgPart[0],
                                resource = msgPart[1]
                            };

                            _logger.Log(LogLevel.Information, $"ReceiveMessage: {ri.resource}");

                            if (!Connection.fromEGS.Any(e => e.resource == ri.resource))
                            {
                                _routedItemManager.Init(ri);
                                _routedItemManager.Enqueue(Connection, Connection.fromEGS, nameof(Connection.fromEGS), copy: false);
                            }
                            else
                            {
                                _logger.Log(LogLevel.Error, $"Resource already exists: {ri.resource}");
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                });

                await hubConnection.StartAsync();
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
            }

            return hubConnection;
        }

        public void OpenBrowser()
        {
            OpenBrowser($"https://{Connection.localHostname}:{Connection.localPort}");
        }

        public void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    // throw 
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }

        public async Task ConnectToHubs()
        {
            try
            {
                // IOptions<HttpConnectionOptions> options = ;
                // options.Value.Headers = 
                // options.Headers
                // options.AccessTokenProvider
                // options.ClientCertificates
                // options.CloseTimeout
                // options.Cookies
                // options.Credentials
                // options.Headers
                // options.HttpMessageHandlerFactory
                // options.Proxy
                // options.SkipNegotiation

                // ILoggerFactory loggerFactory;

                // var factory = new Microsoft.AspNetCore.SignalR.Client.HttpConnectionFactory(options, loggerFactory)

                //look up known EGS instances
                IPHostEntry hostEntry = Dns.GetHostEntry(Connection.remoteHostname);
                foreach (var iPAddress in hostEntry.AddressList)
                {
                    if (!hubConnections.Keys.Contains(iPAddress.ToString()))
                    {
                        HubConnection hubConnection = null;
                        if (iPAddress.AddressFamily == AddressFamily.InterNetworkV6 && Connection.UseIPV6)
                        {
                            hubConnection = await StartHubConnection($"https://[{iPAddress}]:{Connection.remotePort}/chatHub").ConfigureAwait(false);

                        }
                        else if (iPAddress.AddressFamily == AddressFamily.InterNetwork && Connection.UseIPV4)
                        {
                            hubConnection = await StartHubConnection($"https://{iPAddress}:{Connection.remotePort}/chatHub").ConfigureAwait(false);
                        }
                        if (hubConnection != null)
                        {
                            hubConnections.Add(iPAddress.ToString(), hubConnection);
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

        public async Task<bool> ping()
        {
            return await _litePingService.ping(Connection, GetHttpManager());
        }

        protected override void Dispose(bool disposing)
        {
            ToEGSSignal.Dispose();
            FromEGSSignal.Dispose();
            HubMessagesSignal.Dispose();

            base.Dispose(disposing);
        }
    }
}
