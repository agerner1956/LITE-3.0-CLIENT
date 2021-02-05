using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.IoC;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public abstract class ConnectionManager<T> : ConnectionManagerBase<T>, IConnectionRoutedCacheManager, IDisposable
        where T : Connection
    {
        public static Dictionary<string, List<RoutedItem>> cache = new Dictionary<string, List<RoutedItem>>();
        public SemaphoreSlim ToRulesSignal = new SemaphoreSlim(0, 1);
        public readonly Dictionary<string, FileSystemWatcher> FileSystemWatchers = new Dictionary<string, FileSystemWatcher>();
        public readonly BlockingCollection<RoutedItem> toRules = new BlockingCollection<RoutedItem>();

        protected readonly IProfileStorage _profileStorage;
        protected readonly ILiteConfigService _liteConfigService;
        protected readonly IRoutedItemManager _routedItemManager;
        protected readonly IRulesManager _rulesManager;
        protected readonly IRoutedItemLoader _routedItemLoader;
        protected readonly ILITETask _liteTaskManager;

        // To detect redundant calls
        private bool _disposed = false;

        protected ConnectionManager(
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRoutedItemLoader routedItemLoader,
            IRulesManager rulesManager,
            ILITETask liteTaskManager,
            ILogger logger,
            IUtil util)
            : base(logger, util)
        {
            _routedItemManager = routedItemManager;
            _routedItemLoader = routedItemLoader;
            _profileStorage = profileStorage;
            _rulesManager = rulesManager;
            _liteConfigService = liteConfigService;
            _liteTaskManager = liteTaskManager;
        }

        public ILITETask LITETask
        {
            get { return _liteTaskManager; }
        }

        public override void Init()
        {
            lock (InitLock)
            {
                _logger.Log(LogLevel.Information, "Exists Certs Names and Locations");
                _logger.Log(LogLevel.Information, "------ ----- -------------------------");

                if (Connection.useTLS)
                {
                    foreach (StoreLocation storeLocation in (StoreLocation[])
                        Enum.GetValues(typeof(StoreLocation)))
                    {
                        foreach (StoreName storeName in (StoreName[])
                            Enum.GetValues(typeof(StoreName)))
                        {
                            bool entryExists = stores.TryGetValue($"{storeName} + {storeLocation}", out bool exists);
                            if (!entryExists || exists)
                            {
                                X509Store store = new X509Store(storeName, storeLocation);

                                try
                                {
                                    store.Open(OpenFlags.OpenExistingOnly);

                                    stores.TryAdd($"{storeName} + {storeLocation}", true);

                                    _logger.Log(LogLevel.Information, $"Yes {0,4}{store.Certificates.Count}{1}{store.Name}{2}{store.Location}");

                                    foreach (var cert in store.Certificates)
                                    {
                                        _logger.Log(LogLevel.Information, $"Subject: {0,6}{cert.Subject}");
                                    }

                                    store.Close();
                                }
                                catch (CryptographicException)
                                {
                                    stores.TryAdd($"{storeName} + {storeLocation}", false);
                                    _logger.Log(LogLevel.Information, $"No {0}{store.Name} {1}{store.Location}");
                                }
                            }
                        }
                    }
                }

                string dir = null;
                List<string> fileEntries;

                //read the persisted ResponseCache entries
                dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + "ResponseCache" +
                      Path.DirectorySeparatorChar + Constants.Dirs.Cache + Path.DirectorySeparatorChar + Constants.Dirs.Meta;

                Directory.CreateDirectory(dir);
                fileEntries = _util.DirSearch(dir, Constants.Extensions.MetaExt.ToSearchPattern());


                foreach (string file in fileEntries)
                {
                    var st = _routedItemLoader.LoadFromFile(file);
                    if (st == null)
                    {
                        continue;
                    }

                    var list = new List<RoutedItem> { st };
                    var success = cache.TryAdd(st.id, list);
                    if (!success)
                    {
                        cache[st.id].Add(st);
                    }
                }
            }
        }

        public abstract override Task Kickoff(int taskID);

        public abstract override void Stop();

        protected override void ProcessImpl(Connection connection)
        {
        }

        public async Task<bool> PingCert(string URL, int taskID)
        {
            PingCertService pingCertService = new PingCertService(Connection, _liteTaskManager, _logger);
            return await pingCertService.PingCert(URL, taskID);
        }

        public async Task SendToRules(int taskID, bool responsive = true)
        {
            using (var scope = ServiceActivator.GetScope())
            {
                var adapter = scope.ServiceProvider.GetRequiredService<IConnectionToRulesManagerAdapter>();
                var args = new ConnectionToRulesManagerAdapterArgs(Connection, toRules, cache, this);
                await adapter.SendToRules(taskID, args, responsive);
            }
        }

        protected virtual void ToRulesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (ToRulesSignal.CurrentCount == 0) ToRulesSignal.Release();
                }
                catch (Exception)
                {
                } //could be in the middle of being disposed and recreated
            }
        }

        /// <summary>
        ///  result cache where we consolidate multiple routed items based on the same id.The result is added onto the id: key
        ///  which usually arrives in chronological order(Pending, Pending, Success) though not required
        /// </summary>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public RoutedItem CacheResponse(RoutedItem routedItem)
        {
            using (var scope = ServiceActivator.GetScope())
            {
                var cacheService = scope.ServiceProvider.GetRequiredService<IConnectionCacheResponseService>();
                return cacheService.CacheResponse(Connection, routedItem, cache);
            }           
        }

        public void RemoveCachedItem(RoutedItem routedItem)
        {
            using (var scope = ServiceActivator.GetScope())
            {
                var cacheService = scope.ServiceProvider.GetRequiredService<IConnectionCacheResponseService>();
                cacheService.RemoveCachedItem(Connection, routedItem, cache);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (toRules != null)
                {
                    toRules.Dispose();
                }

                if (ToRulesSignal != null)
                {
                    ToRulesSignal.Dispose();
                }
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            _disposed = true;
        }
    }
}
