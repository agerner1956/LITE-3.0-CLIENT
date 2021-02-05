using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Lite.Core.Common;
using Serilog;
using System;
using System.IO;
using Serilog.Sinks.SystemConsole.Themes;

namespace Lite3
{
    public static class SerilogConfigurationExtensions
    {
        public const string loggerTemplate = @"{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}]<{ThreadId}> [{SourceContext:l}] {Message:lj}{NewLine}{Exception}";

        public static Serilog.ILogger GetSerilogLogger(this IConfiguration configuration)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logDir = Path.Combine(baseDir, "Logs");
            var logfile = Path.Combine(logDir, "log.txt");

            Serilog.LoggerConfiguration loggerConfig =
                new LoggerConfiguration()
                .ReadFrom.Configuration(configuration, "Logging");

            var splunkSection = configuration.GetSection("CommonSettings:SplunkConfig");
            SplunkConfig splunkConfig = new SplunkConfig();
            splunkSection.Bind(splunkConfig);

            var cfg = loggerConfig
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: loggerTemplate, theme: AnsiConsoleTheme.Literate)
                .WriteTo.Debug()
                .WriteTo.File(logfile, outputTemplate: loggerTemplate, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90);

            if (splunkConfig.IsValid())
            {
                cfg = cfg.WriteTo.EventCollector(splunkConfig.Uri, splunkConfig.Token);
            }

            Log.Logger = cfg.CreateLogger();

            return Log.Logger;
        }
    }
}
