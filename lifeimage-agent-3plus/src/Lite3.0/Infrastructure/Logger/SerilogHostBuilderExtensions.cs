using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lite3
{
    public static class SerilogHostBuilderExtensions
    {
        public static IHostBuilder UseSerilog(this IHostBuilder builder,
            Serilog.ILogger logger = null, bool dispose = false)
        {
            builder.ConfigureServices((context, collection) =>
                collection.AddSingleton<ILoggerFactory>(services => new SerilogLoggerFactory(logger, dispose)));
            return builder;
        }
    }
}
