using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Services.Connections.Dcmtk;
using Lite.Services.Connections.Dicom;
using Lite.Services.Connections.Files;
using Lite.Services.Connections.Hl7;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Lite.Services.Connections
{
    public sealed class ConnectionManagerFactory : IConnectionManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public ConnectionManagerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IConnectionManager GetManager(Connection connection)
        {
            IConnectionManager result = null;
            switch (connection.connType)
            {
                case ConnectionType.cloud:
                    result = _serviceProvider.GetRequiredService<ILifeImageCloudConnectionManager>();
                    break;

                case ConnectionType.dcmtk:
                    result = _serviceProvider.GetRequiredService<IDcmtkConnectionManager>();
                    break;

                case ConnectionType.dicom:
                    result = _serviceProvider.GetRequiredService<IDicomConnectionManager>();
                    break;

                case ConnectionType.hl7:
                    result = _serviceProvider.GetRequiredService<IHl7ConnectionManager>();
                    break;

                case ConnectionType.file:
                    result = _serviceProvider.GetRequiredService<IFileConnectionManager>();
                    break;

                case ConnectionType.lite:
                    result = _serviceProvider.GetRequiredService<ILiteConnectionManager>();
                    break;

                default:
                    break;
            }

            if (result != null)
            {
                result.Load(connection);
            }

            return result;
        }
    }
}
