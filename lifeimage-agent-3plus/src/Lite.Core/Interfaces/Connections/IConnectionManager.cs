using Lite.Core.Models;
using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface IConnectionManager
    {
        void Load(Connections.Connection connection);
        void Init();
        void Stop();
        Task Kickoff(int taskID);
    }

    public interface IConnectionRoutedCacheManager
    {
        RoutedItem Route(RoutedItem routedItem, bool copy = false);
        RoutedItem CacheResponse(RoutedItem routedItem);
        void RemoveCachedItem(RoutedItem routedItem);
    }
}
