using Lite.Core.Enums;
using Lite.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface IRulesManager
    {
        Rules Item { get; }
        void Init(Rules item);

        Task<Priority> CheckAndDelayOnWaitConditions(RoutedItem routedItem);
        void DisengageWaitConditions(RoutedItem routedItem);
        bool DoesRouteDestinationExistForSource(string source);
        List<ConnectionSet> Eval(RoutedItem routedItem);
        Task<RoutedItem> SendToRules(RoutedItem ri, IRoutedItemManager routedItemManager, IConnectionRoutedCacheManager connectionRoutedCacheManager);
    }
}
