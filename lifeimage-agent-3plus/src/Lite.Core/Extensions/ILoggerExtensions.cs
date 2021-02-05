using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lite.Core
{
    public static class ILoggerExtensions
    {
        [System.Obsolete]
        public static void Log(this ILogger logger, TraceEventType traceEventType, string msg)
        {
            LogLevel logLevel = LogLevel.Debug;

            switch (traceEventType)
            {
                case TraceEventType.Error:
                    logLevel = LogLevel.Error;
                    break;

                case TraceEventType.Warning:
                    logLevel = LogLevel.Warning;
                    break;

                case TraceEventType.Verbose:
                    logLevel = LogLevel.Debug;
                    break;

                case TraceEventType.Critical:
                    logLevel = LogLevel.Critical;
                    break;
            }

            logger.Log(logLevel, msg);            
        }
    }
}
