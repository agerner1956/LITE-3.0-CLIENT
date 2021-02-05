using System.Diagnostics;

namespace Lite.Core.Models
{
    public class SplunkFilter : TraceFilter
    {
        private Logger logger;

        public SplunkFilter(Logger logger)
        {
            this.logger = logger;
        }

        public override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, string formatOrMessage, object[] args, object data1, object[] data)
        {
            //splunk (TraceSource)
            switch (logger.GetSplunkTraceLevelValue())
            {
                case Logger.TraceLevel.Off:
                    break;
                case Logger.TraceLevel.Error:
                    if (eventType <= TraceEventType.Error)
                    {
                        return true;
                    }
                    break;
                case Logger.TraceLevel.Warning:
                    if (eventType <= TraceEventType.Warning)
                    {
                        return true;
                    }
                    break;
                case Logger.TraceLevel.Info:
                    if (eventType <= TraceEventType.Information)
                    {
                        return true;
                    }
                    break;
                case Logger.TraceLevel.Verbose:
                    // if (tracePattern != null && tracePattern != "")
                    // {
                    //     if (Regex.Matches(message, tracePattern).Count > 0)
                    //     {
                    //         trace.TraceEvent(TraceEventType.Verbose, eventID++, message);
                    //     }
                    // }
                    //else 
                    if (eventType <= TraceEventType.Verbose)
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }
    }
}
