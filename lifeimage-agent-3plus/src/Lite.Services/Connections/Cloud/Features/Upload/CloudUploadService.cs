using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ICloudUploadService
    {
        Task Upload(int taskID, LifeImageCloudConnection Connection, ILifeImageCloudConnectionManager manager, IConnectionRoutedCacheManager cache, IHttpManager httpManager);
    }

    public sealed class CloudUploadService : ICloudUploadService
    {
        private readonly ICloudAgentTaskLoader _cloudAgentTaskLoader;        
        private readonly IProfileStorage _profileStorage;
        private readonly IRulesManager _rulesManager;                   
        private readonly IPostResponseCloudService _postResponseCloudService;       
        private readonly ISendToCloudService _sendToCloudService;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public CloudUploadService(
            ICloudAgentTaskLoader cloudAgentTaskLoader,
            IProfileStorage profileStorage,
            IRulesManager rulesManager,                       
            IPostResponseCloudService postResponseCloudService,            
            ISendToCloudService sendToCloudService,
            ILITETask taskManager,
            ILogger<CloudUploadService> logger)
        {
            _cloudAgentTaskLoader = cloudAgentTaskLoader;
            _profileStorage = profileStorage;
            _rulesManager = rulesManager;                                  
            _postResponseCloudService = postResponseCloudService;            
            _sendToCloudService = sendToCloudService;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task Upload(int taskID, LifeImageCloudConnection Connection, ILifeImageCloudConnectionManager manager, IConnectionRoutedCacheManager cache, IHttpManager httpManager)
        {            
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering Upload");

            try
            {
                bool success = await manager.ToCloudSignal.WaitAsync(_profileStorage.Current.KickOffInterval, _taskManager.cts.Token)
                    .ConfigureAwait(false);
                // ToCloudSignal.Dispose();
                // ToCloudSignal = new SemaphoreSlim(0, 1);

                await _sendToCloudService.SendToCloud(taskID, Connection, cache, httpManager);

                //if (_profileStorage.Current.rules.DoesRouteDestinationExistForSource(Connection.name))
                if (_rulesManager.DoesRouteDestinationExistForSource(Connection.name))
                {
                    if (_taskManager.CanStart($"{Connection.name}.GetRequests"))
                    {
                        var newTaskID = _taskManager.NewTaskID();
                        Task task = new Task(new Action(async () => await _cloudAgentTaskLoader.GetRequests(taskID, Connection, cache, httpManager)), _taskManager.cts.Token);
                        await _taskManager.Start(newTaskID, task, $"{Connection.name}.GetRequests", isLongRunning: false);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task was canceled.");
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} Wait Operation Canceled. Exiting Upload");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
                _logger.Log(LogLevel.Critical, $"{taskInfo} Exiting Upload");
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.Upload");
            }
        }
    }   
}
