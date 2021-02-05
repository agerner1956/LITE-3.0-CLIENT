using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lite.Core
{
    public class Logger //: Dicom.Log.Logger
    {
        public enum TraceLevel
        {
            //
            // Summary:
            //     Output no tracing and debugging messages.
            Off = 0,
            //
            // Summary:
            //     Output error-handling messages.
            Error = 1,
            //
            // Summary:
            //     Output warnings and error-handling messages.
            Warning = 2,
            //
            // Summary:
            //     Output informational messages, warnings, and error-handling messages.
            Info = 3,
            //
            // Summary:
            //     Output all debugging and tracing messages.
            Verbose = 4
        }

        /// <summary>
        /// Off, Error, Warning, Info, Verbose  
        /// </summary>
        [JsonPropertyName("ConsoleTraceLevel")]
        [EnumDataType(typeof(TraceLevel))]
        public string ConsoleTraceLevel { get; set; } = "Warning";

        /// <summary>
        /// Off, Error, Warning, Info, Verbose    
        /// </summary>
        [JsonPropertyName("FileTraceLevel")]
        [EnumDataType(typeof(TraceLevel))]
        public string FileTraceLevel { get; set; } = "Verbose";

        /// <summary>
        /// Off, Error, Warning, Info, Verbose.
        /// </summary>
        [JsonPropertyName("SplunkTraceLevel")]
        [EnumDataType(typeof(TraceLevel))]
        public string SplunkTraceLevel { get; set; } = "Info";

        [JsonPropertyName("TracePattern")]
        public string TracePattern { get; set; }

        [NonSerialized()]
        private static TraceSource ConsoleTrace = new TraceSource("LITE");

        [NonSerialized()]
        private static TraceSource FileTrace = new TraceSource("LITE");

        [NonSerialized()]
        private static TraceSource SplunkTrace = new TraceSource("LITE");

        [NonSerialized()]
        public static Logger logger = new Logger();// new Logger("");

        //[NonSerialized()]
        //public static HttpEventCollectorTraceListener splunk;

        [NonSerialized()]
        public static TextWriterTraceListener TextWriterTraceListener = new TextWriterTraceListener();

        [NonSerialized()]
        public static DefaultTraceListener ConsoleTraceListener = new DefaultTraceListener();

        [NonSerialized()]
        public static string LoggingFileName;

        [NonSerialized()]
        public string accountInfo = "";

        //[NonSerialized()]
        //public HttpEventCollectorEventInfo.Metadata metadata = null;

        [NonSerialized()]
        private int FileEventID = 0;

        [NonSerialized()]
        private int SplunkEventID = 0;

        [NonSerialized()]
        private object RotateLogsLock = new Object();

        ~Logger()
        {
            // trace.Close();
            // trace = null;
        }

        /// <summary>
        ///  Get log level as an enum
        /// </summary>
        /// <returns></returns>
        public TraceLevel GetConsoleTraceLevelValue()
        {
            for (int index = (int)TraceLevel.Off; index <= (int)TraceLevel.Verbose; index++)
            {
                if (Enum.GetName(typeof(TraceLevel), index) == ConsoleTraceLevel)
                    return (TraceLevel)index;
            }

            // Just default to info
            return TraceLevel.Info;
        }

        /// <summary>
        ///  Get log level as an enum
        /// </summary>
        /// <returns></returns>
        public TraceLevel GetFileTraceLevelValue()
        {
            for (int index = (int)TraceLevel.Off; index <= (int)TraceLevel.Verbose; index++)
            {
                if (Enum.GetName(typeof(TraceLevel), index) == FileTraceLevel)
                    return (TraceLevel)index;
            }

            // Just default to info
            return TraceLevel.Info;
        }

        /// <summary>
        ///  Get log level as an enum
        /// </summary>
        /// <returns></returns>
        public TraceLevel GetSplunkTraceLevelValue()
        {
            for (int index = (int)TraceLevel.Off; index <= (int)TraceLevel.Verbose; index++)
            {
                if (Enum.GetName(typeof(TraceLevel), index) == SplunkTraceLevel)
                    return (TraceLevel)index;
            }

            // Just default to info
            return TraceLevel.Info;
        }

        public void Log(TraceEventType level, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            switch (level)
            {
                case TraceEventType.Critical:
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.ForegroundColor = ConsoleColor.Black;

                    break;
                case TraceEventType.Error:
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Red;

                    break;
                case TraceEventType.Information:
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.White;

                    break;
                case TraceEventType.Resume:
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.Green;

                    break;
                case TraceEventType.Start:
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.DarkGreen;

                    break;
                case TraceEventType.Stop:
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.DarkRed;

                    break;
                case TraceEventType.Suspend:
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.DarkYellow;

                    break;
                case TraceEventType.Transfer:
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.DarkCyan;

                    break;
                case TraceEventType.Verbose:
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
                case TraceEventType.Warning:
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
            }

            //append timestamp and level
            message = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff")} {level} {sourceFilePath.Substring(sourceFilePath.LastIndexOfAny(new char[] { '/', '\\' }) + 1)}.{memberName}:{sourceLineNumber}:{(Thread.CurrentThread.Name ?? "Worker")}:{Thread.CurrentThread.ManagedThreadId} {message}";

            //(TraceSource)
            switch (GetConsoleTraceLevelValue())
            {
                case TraceLevel.Off:
                    break;
                case TraceLevel.Error:
                    if (level <= TraceEventType.Error)
                    {
                        ConsoleTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
                case TraceLevel.Warning:
                    if (level <= TraceEventType.Warning)
                    {
                        ConsoleTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
                case TraceLevel.Info:
                    if (level <= TraceEventType.Information)
                    {
                        ConsoleTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
                case TraceLevel.Verbose:
                    if (TracePattern != null && TracePattern != "")
                    {
                        if (Regex.Matches(message, TracePattern).Count > 0)
                        {
                            ConsoleTrace.TraceEvent(level, FileEventID++, message);
                        }
                    }
                    else if (level <= TraceEventType.Verbose)
                    {
                        ConsoleTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
            }

            //(TraceSource)
            switch (GetFileTraceLevelValue())
            {
                case TraceLevel.Off:
                    break;
                case TraceLevel.Error:
                    if (level <= TraceEventType.Error)
                    {
                        FileTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
                case TraceLevel.Warning:
                    if (level <= TraceEventType.Warning)
                    {
                        FileTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
                case TraceLevel.Info:
                    if (level <= TraceEventType.Information)
                    {
                        FileTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
                case TraceLevel.Verbose:
                    if (TracePattern != null && TracePattern != "")
                    {
                        if (Regex.Matches(message, TracePattern).Count > 0)
                        {
                            FileTrace.TraceEvent(level, FileEventID++, message);
                        }
                    }
                    else if (level <= TraceEventType.Verbose)
                    {
                        FileTrace.TraceEvent(level, FileEventID++, message);
                    }
                    break;
            }

            //(TraceSource)
            switch (GetSplunkTraceLevelValue())
            {
                case TraceLevel.Off:
                    break;
                case TraceLevel.Error:
                    if (level <= TraceEventType.Error)
                    {
                        SplunkTrace.TraceEvent(level, SplunkEventID++, message);
                    }
                    break;
                case TraceLevel.Warning:
                    if (level <= TraceEventType.Warning)
                    {
                        SplunkTrace.TraceEvent(level, SplunkEventID++, message);
                    }
                    break;
                case TraceLevel.Info:
                    if (level <= TraceEventType.Information)
                    {
                        SplunkTrace.TraceEvent(level, SplunkEventID++, message);
                    }
                    break;
                case TraceLevel.Verbose:
                    if (TracePattern != null && TracePattern != "")
                    {
                        if (Regex.Matches(message, TracePattern).Count > 0)
                        {
                            SplunkTrace.TraceEvent(level, SplunkEventID++, message);
                        }
                    }
                    else if (level <= TraceEventType.Verbose)
                    {
                        SplunkTrace.TraceEvent(level, SplunkEventID++, message);
                    }
                    break;
            }
        }

        //public override void Log(Dicom.Log.LogLevel level, string msg, params object[] args)
        //{
        //    // public enum LogLevel
        //    // {
        //    //     Debug = 0,
        //    //     Info = 1,
        //    //     Warning = 2,
        //    //     Error = 3,
        //    //     Fatal = 4
        //    // }
        //    TraceEventType traceEventType;
        //    switch (level)
        //    {
        //        case Dicom.Log.LogLevel.Debug:
        //            traceEventType = TraceEventType.Verbose;
        //            break;
        //        case Dicom.Log.LogLevel.Info:
        //            traceEventType = TraceEventType.Information;
        //            break;
        //        case Dicom.Log.LogLevel.Warning:
        //            traceEventType = TraceEventType.Warning;
        //            break;
        //        case Dicom.Log.LogLevel.Error:
        //            traceEventType = TraceEventType.Error;
        //            break;
        //        case Dicom.Log.LogLevel.Fatal:
        //            traceEventType = TraceEventType.Error;
        //            break;
        //        default:
        //            traceEventType = TraceEventType.Verbose;
        //            break;
        //    }

        //    string[] arr = ((IEnumerable)args).Cast<object>()
        //                         .Select(x => x.ToString())
        //                         .ToArray();
        //    Log(traceEventType, $"fo-dicom: {msg}{String.Join("|", arr)}");
        //}
    }
}
