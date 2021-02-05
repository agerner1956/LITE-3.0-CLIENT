using Lite.Core.Connections;
using Lite.Core.Interfaces;

namespace Lite.Services.Connections
{
    public interface IConnectionManagerFactory
    {
        IConnectionManager GetManager(Connection connection);
    }
}
