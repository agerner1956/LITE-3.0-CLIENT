using Lite.Core.Connections;
using Lite.Core.Enums;
using System.Collections.Generic;

namespace Lite.Core.Interfaces
{
    public interface IConnectionFinder
    {
        Connection GetConnectionByName(Profile profile, string name);
        Connection GetConnectionByType(Profile profile, ConnectionType eType);
        List<Connection> GetConnectionsByType(Profile profile, ConnectionType eType);
        List<LifeImageCloudConnection> GetLifeimageCloudConnections(Profile profile);
        LITEConnection GetLITEConnection(Profile profile);
        LITEConnection GetPrimaryLITEConnection(Profile profile);
        LifeImageCloudConnection GetPrimaryLifeImageConnection(Profile profile);
        DICOMConnection GetDicomConnectionToLocalAETitle(Profile profile, string localAETitle);
    }
}
