using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lite.Services
{
    public sealed class ConnectionFinder : IConnectionFinder
    {
        private readonly ILogger _logger;

        public ConnectionFinder(
            ILogger<ConnectionFinder> logger)
        {
            _logger = logger;
        }

        public Connection GetConnectionByName(Profile profile, string name)
        {
            Throw.IfNull(profile);

            if (name == null)
                return null;

            return profile.connections.Find(a => a.name == name);
        }

        public Connection GetConnectionByType(Profile profile, ConnectionType eType)
        {
            Throw.IfNull(profile);

            return profile.connections.Find(a => a.connType == eType);
        }

        public List<Connection> GetConnectionsByType(Profile profile, ConnectionType eType)
        {
            Throw.IfNull(profile);

            return profile.connections.FindAll(a => a.connType == eType);
        }

        public List<LifeImageCloudConnection> GetLifeimageCloudConnections(Profile profile)
        {
            Throw.IfNull(profile);

            return profile.connections.FindAll(a => a.connType == ConnectionType.cloud && a.enabled).Cast<LifeImageCloudConnection>().ToList();
        }

        public LITEConnection GetLITEConnection(Profile profile)
        {
            Throw.IfNull(profile);

            return (LITEConnection)profile.connections.Find(a => a.connType == ConnectionType.lite && a.enabled);
        }

        public LifeImageCloudConnection GetPrimaryLifeImageConnection(Profile profile)
        {
            Throw.IfNull(profile);
            return (LifeImageCloudConnection)profile.connections.Find(a => a.connType == ConnectionType.cloud && ((LifeImageCloudConnection)a).isPrimary);
        }

        public DICOMConnection GetDicomConnectionToLocalAETitle(Profile profile, string localAETitle)
        {
            Throw.IfNull(profile);

            List<Connection> connectionList = profile.connections.FindAll(a => a.connType == ConnectionType.dicom &&
                                                                 ((DICOMConnection)a).localAETitle == localAETitle && a.enabled == true);

            if (connectionList == null || connectionList.Count == 0)
            {
                List<Connection> tempList = profile.connections.FindAll(a => a.connType == ConnectionType.dicom && a.enabled == true);
                foreach (var dicom in tempList)
                {
                    _logger.Log(LogLevel.Debug, $"GetDicomConnectionToCallingAETitle: Inbound AETitle: {localAETitle} does not match {((DICOMConnection)dicom).localAETitle}");
                }
            }
            else if (connectionList.Count > 1)
            {
               _logger.Log(LogLevel.Warning, $"GetDicomConnectionToCallingAETitle: Inbound AETitle: {localAETitle} matches to more than one DICOMConnection.  Only the first one will be used.");

                foreach (var dicom in connectionList)
                {
                    _logger.Log(LogLevel.Debug, $"GetDicomConnectionToCallingAETitle: Inbound AETitle: {localAETitle} matches {dicom.name}.{((DICOMConnection)dicom).localAETitle}");
                }
            }

            return (connectionList.Count > 0) ? (DICOMConnection)(connectionList[0]) : null;
        }
    }
}