using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.IoC;
using Lite.Core.Utils;
using Lite.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public interface IHttpManager
    {
        bool loginNeeded { get; set; }
        public string jSessionID { get; set; }
    }

    public interface IHttpConnectionManager : IConnectionManager, IHttpManager
    {
        ILiteHttpClient LiteHttpClient { get; }
    }

    public abstract class HttpConnectionManagerBase<T> : ConnectionManager<T>, IHttpConnectionManager where T : HttpConnection
    {
        private ILiteHttpClient _liteHttpClient;

        protected HttpConnectionManagerBase(
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRoutedItemLoader routedItemLoader,
            IRulesManager rulesManager,
            ILITETask lITETask,
            ILogger logger,
            IUtil util) : base(
                profileStorage,
                liteConfigService,
                routedItemManager,
                routedItemLoader,
                rulesManager,
                lITETask,
                logger,
                util)
        {
        }

        public IHttpManager GetHttpManager()
        {
            return this;
        }

        public ILiteHttpClient LiteHttpClient => _liteHttpClient;

        public bool loginNeeded { get; set; }

        public string jSessionID { get; set; }

        public override void Init()
        {
            var serviceScope = ServiceActivator.GetScope();
            ILoggerFactory loggerFactory = serviceScope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            _liteHttpClient = new LiteHttpClient(_profileStorage, loggerFactory.CreateLogger<LiteHttpClient>());
        }

        public override abstract Task Kickoff(int taskID);

        public override abstract void Stop();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
