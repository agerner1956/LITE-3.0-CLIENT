using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Lite.Features
{
    public interface ILiteUploadService
    {
        Task Upload(int taskID, LITEConnection connection, SemaphoreSlim toEGSSignal, ISendToAllHubsService sendToAllHubsService);
    }

    public sealed class LiteUploadService : ILiteUploadService
    {
        private readonly ILiteToEgsService _liteToEgsService;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public LiteUploadService(
            ILiteToEgsService liteToEgsService,
            IProfileStorage profileStorage,
            ILITETask taskManager,
            ILogger<LiteUploadService> logger)
        {
            _liteToEgsService = liteToEgsService;
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _logger = logger;
        }

        public LITEConnection Connection { get; set; }
        public SemaphoreSlim ToEGSSignal { get; set; }

        public ISendToAllHubsService SendToAllHubsService { get; set; }

        public async Task Upload(int taskID, LITEConnection connection, SemaphoreSlim toEGSSignal, ISendToAllHubsService sendToAllHubsService)
        {
            Connection = connection;
            ToEGSSignal = toEGSSignal;
            SendToAllHubsService = sendToAllHubsService;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
    
            _logger.Log(LogLevel.Debug, $"{taskInfo} Entering Upload");

            try
            {
                await UploadImpl(taskID);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevel.Warning, $"Wait Operation Canceled. Exiting Upload");
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

        private async Task UploadImpl(int taskID)
        {
            var profile = _profileStorage.Current;
            do
            {
                bool success = await ToEGSSignal.WaitAsync(profile.KickOffInterval, _taskManager.cts.Token).ConfigureAwait(false);

                await Task.Delay(1000).ConfigureAwait(false);  //to allow for some accumulation for efficient batching.

                await _liteToEgsService.SendToEGS(taskID, Connection, SendToAllHubsService);

            } while (Connection.responsive);
        }
    }
}
