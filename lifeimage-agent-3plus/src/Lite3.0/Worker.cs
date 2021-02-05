using Lite.Core.Common;
using Lite.Core.Interfaces;
using Lite.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lite3
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly CommonSettings _settingsRegistry;
        private readonly IServiceProvider _services;
        private readonly ILiteEngine _liteEngine;

        public Worker(
            ILiteEngine liteEngine,
            IOptions<CommonSettings> settings,
            ILogger<Worker> logger,             
            IServiceProvider services)
        {
            _liteEngine = liteEngine;
            _settingsRegistry = settings.Value;
            _services = services;
            _logger = logger;            
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _liteEngine.Process();
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Could not process service. Re-run..");

                    await _liteEngine.Restart();
                }
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing LITE engine");
            //_liteEngine.init();

            _logger.LogInformation("Starting LITE engine...");

            _liteEngine.Start();
            return base.StartAsync(cancellationToken);
        }

        protected void Input_OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                _logger.LogInformation($"InBound Change Event Triggered by [{e.FullPath}]");

                // do some work
                using (var scope = _services.CreateScope())
                {
                    //var serviceA = scope.ServiceProvider.GetRequiredService<IServiceA>();
                    //serviceA.Run();
                }

                _logger.LogInformation("Done with Inbound Change Event");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Service");
            _liteEngine.Stop();
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing Service");
            _liteEngine.Dispose();
            base.Dispose();
        }
    }
}
