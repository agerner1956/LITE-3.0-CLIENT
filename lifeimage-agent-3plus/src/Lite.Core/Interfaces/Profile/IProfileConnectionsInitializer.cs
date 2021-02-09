using System.Collections.Generic;

namespace Lite.Core.Interfaces
{
    public interface IProfileConnectionsInitializer
    {
        void InitConnections(Profile profile, List<string> argsList, object ProfileLocker);
    }
}
