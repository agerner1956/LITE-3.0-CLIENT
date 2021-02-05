using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public sealed class ConnectionManagerConcrete : ConnectionManager<Connection>
    {
        public ConnectionManagerConcrete(
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRulesManager rulesManager,
            IRoutedItemLoader routedItemLoader,
            ILITETask taskManager,
            ILogger logger,
            IUtil util)
            : base(profileStorage, liteConfigService, routedItemManager, routedItemLoader, rulesManager, taskManager, logger, util)
        {
        }

        public override Task Kickoff(int taskID)
        {
            throw new System.NotImplementedException();
        }

        public override RoutedItem Route(RoutedItem routedItem, bool copy = false)
        {
            throw new System.NotImplementedException();
        }

        public override void Stop()
        {
            throw new System.NotImplementedException();
        }
    } 
}
