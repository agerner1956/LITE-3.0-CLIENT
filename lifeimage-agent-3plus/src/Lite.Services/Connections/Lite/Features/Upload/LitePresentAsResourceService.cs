using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILitePresentAsResourceService
    {
        Task PresentAsResource(List<RoutedItem> batch, int taskID, LITEConnection connection, ISendToAllHubsService sendToAllHubs);
    }

    public sealed class LitePresentAsResourceService : ILitePresentAsResourceService
    {
        private readonly IRoutedItemManager _routedItemManager;        
        private readonly ILogger _logger;

        public LitePresentAsResourceService(
            IRoutedItemManager routedItemManager,            
            ILogger<LitePresentAsResourceService> logger)
        {
            _routedItemManager = routedItemManager;            
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }

        public async Task PresentAsResource(List<RoutedItem> batch, int taskID, LITEConnection connection, ISendToAllHubsService sendToAllHubs)
        {
            Connection = connection;
            try
            {
                var path = Connection.resourcePath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "Resource" + Path.DirectorySeparatorChar + "batch" + Path.DirectorySeparatorChar + System.Guid.NewGuid();
                Directory.CreateDirectory(path);

                //Move all files in batch to separate folder
                foreach (var ri in batch)
                {
                    File.Move(ri.sourceFileName, path + Path.DirectorySeparatorChar + ri.sourceFileName.Substring(ri.sourceFileName.LastIndexOf(Path.DirectorySeparatorChar)));
                    ri.sourceFileName = path + Path.DirectorySeparatorChar + ri.sourceFileName.Substring(ri.sourceFileName.LastIndexOf(Path.DirectorySeparatorChar));
                }

                var file = path + ".zip";

                //zip up files and meta
                ZipFile.CreateFromDirectory(path, file);


                //protect the file
                //var protected file = Crypto.Protect(file)


                //let EGGS know it's available, or when we convert udt to .net core then perhaps push so no open socket required on client.
                await sendToAllHubs.SendToAllHubs(X509CertificateService.ServicePointName, file);

                //Dequeue
                foreach (var ri in batch)
                {
                    _routedItemManager.Init(ri);
                    _routedItemManager.Dequeue(Connection, Connection.toEGS, nameof(Connection.toEGS), error: false);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
            }
        }
    }
}
