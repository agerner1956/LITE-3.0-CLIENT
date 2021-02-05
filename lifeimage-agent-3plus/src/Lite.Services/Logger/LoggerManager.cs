using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LoggerManager : ILoggerManager
    {
        private readonly ILogger _logger;
        private readonly IUtil _util;

        public LoggerManager(
            IUtil util,
            ILogger<LoggerManager> logger)
        {
            _util = util;
            _logger = logger;
        }

        public Logger Init(Logger logger, string account=null)
        {
            //if (logger == null)
            //{
            //    logger = new Logger();

            //    //for testing during source debug...
            //    //            var appenders = LogManager.GetLogger(ilog.GetType()).Logger.Repository.GetAppenders();

            //    var hostname = Dns.GetHostName();
            //    var host = Dns.GetHostEntry(hostname);
            //    foreach (var ip in host.AddressList)
            //    {
            //        hostname += " " + ip.ToString();
            //    }

            //    try
            //    {
            //        var primary = LITE.profile.GetPrimaryLifeImageConnection();
            //        if (primary != null)
            //        {
            //            accountInfo = primary.organizationCode + " " + primary.username + " " + LITE.version + " ";
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        accountInfo = LITE.version;
            //    }

            //    //Splunk via TraceSource
            //    metadata = new Splunk.Logging.HttpEventCollectorEventInfo.Metadata(source: "LITE", sourceType: "SDTS", host: accountInfo + " " + hostname + " ");

            //    //uri /services/collector/event
            //    splunk = new HttpEventCollectorTraceListener(uri: new Uri("https://http-inputs-lifeimage.splunkcloud.com:443"), token: "3162FAE2-21B2-43A5-8118-4C924DE4A0CA",
            //          metadata: metadata, sendMode: HttpEventCollectorSender.SendMode.Sequential, batchInterval: 10000,
            //                batchSizeBytes: 10240000, batchSizeCount: 10);

            //    var path = Util.GetTempFolder() + Path.DirectorySeparatorChar + "log";
            //    Console.WriteLine($"Logging path: {path}");
            //    Directory.CreateDirectory(path);
            //    // AMG 07-31-2020 TIMESTAMP NEEDS to be removed from log name. RotateLogs method has to be refactored. I can't understand why log4net standard logging was overwritten by homemade solution.  
            //    //LoggingFileName = path + Path.DirectorySeparatorChar + "LITE" + ".log";
            //    LoggingFileName = path + Path.DirectorySeparatorChar + "LITE-" + DateTime.Now.ToString("yyyy'-'MM'-'dd HH'-'mm'-'ss'-'ffff") + ".log";

            //    Stream stream = File.Open(LoggingFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            //    TextWriter LogStream = new StreamWriter(stream);
            //    TextWriterTraceListener = new TextWriterTraceListener(LogStream);
            //    ConsoleTraceListener = new DefaultTraceListener();

            //    //                DefaultTraceListener = new System.Diagnostics.DefaultTraceListener();
            //    //                DefaultTraceListener.LogFileName = LoggingFileName + "Default";

            //    splunk.AddLoggingFailureHandler((ex) =>
            //    {
            //        if (ex.InnerException != null && ex.InnerException.Message != null)
            //        {
            //            _logger.Log(TraceEventType.Error, $"{ex.Message} {ex.StackTrace} Inner Exception: {ex.InnerException.Message}");
            //            if (ex.InnerException.StackTrace != null)
            //            {
            //                _logger.Log(TraceEventType.Error, $"{ex.Message} {ex.StackTrace} Inner Exception StackTrace: {ex.InnerException.StackTrace}");
            //            }
            //        }
            //        else
            //        {
            //            _logger.Log(TraceEventType.Error, $"{ex.Message} {ex.StackTrace}");
            //        }

            //    });

            //    splunk.Filter = new SplunkFilter(logger);

            //    SplunkTrace.Listeners.Add(splunk);
            //    FileTrace.Listeners.Add(TextWriterTraceListener);
            //    ConsoleTrace.Listeners.Add(ConsoleTraceListener);

            //    //                trace.Listeners.Add(DefaultTraceListener);
            //    Trace.AutoFlush = true;
            //    ConsoleTrace.Switch = new SourceSwitch("SourceSwitch", "All");
            //    ConsoleTrace.Switch.Level = System.Diagnostics.SourceLevels.All;
            //    FileTrace.Switch = new SourceSwitch("SourceSwitch", "All");
            //    FileTrace.Switch.Level = System.Diagnostics.SourceLevels.All;
            //    SplunkTrace.Switch = new SourceSwitch("SourceSwitch", "All");
            //    SplunkTrace.Switch.Level = System.Diagnostics.SourceLevels.All;

            //    Task.Run(RotateLogs);
            //}

            return logger;
        }

        public async Task RotateLogs()
        {
            //try
            //{
            //    var logPath = _util.GetTempFolder() + Path.DirectorySeparatorChar + "log";
            //    while (true)
            //    {
            //        await Task.Delay(60000);
            //        if (LITE.profile != null)
            //        {
            //            if (File.Exists(LoggingFileName))
            //            {
            //                FileInfo fileInfo = new FileInfo(LoggingFileName);
            //                if (fileInfo.Length > LITE.profile.LogFileSize)
            //                {
            //                    lock (RotateLogsLock)
            //                    {
            //                        FileTrace.Close();
            //                        LoggingFileName = _util.GetTempFolder() + Path.DirectorySeparatorChar + "log" + Path.DirectorySeparatorChar + "LITE-" + DateTime.Now.ToString("yyyy'-'MM'-'dd HH'-'mm'-'ss'-'ffff") + ".log";
            //                        Stream stream = File.Open(LoggingFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            //                        TextWriter LogStream = new StreamWriter(stream);
            //                        TextWriterTraceListener.Writer = LogStream;
            //                        FileTrace.Listeners.Clear();
            //                        FileTrace.Listeners.Add(TextWriterTraceListener);
            //                    }
            //                }
            //            }


            //            //purge logPath

            //            foreach (var file in _util.DirSearch(logPath, "*.*"))
            //            {

            //                try
            //                {
            //                    if (File.Exists(file))
            //                    {
            //                        var attr = File.GetAttributes(file);
            //                        if (!attr.HasFlag(FileAttributes.Hidden))
            //                        {
            //                            var lastaccesstime = File.GetLastAccessTime(file);
            //                            var creationtime = File.GetCreationTime(file);
            //                            var lastwritetime = File.GetLastWriteTime(file);
            //                            var purgetime = DateTime.Now.AddDays(LITE.profile.logRetentionDays * -1);
            //                            if (lastwritetime.CompareTo(purgetime) < 0
            //                            && lastaccesstime.CompareTo(purgetime) < 0
            //                            && creationtime.CompareTo(purgetime) < 0)
            //                            {
            //                                _logger.Log(TraceEventType.Verbose, $"Purging: {file}");
            //                                try
            //                                {
            //                                    File.Delete(file);
            //                                }
            //                                catch (Exception e)
            //                                {
            //                                    Logger.logger.Log(TraceEventType.Critical, $"{e.Message} {e.StackTrace}");
            //                                    if (e.InnerException != null)
            //                                    {
            //                                        Logger.logger.Log(TraceEventType.Critical, $"Inner Exception: {e.InnerException}");
            //                                    }
            //                                }

            //                            }
            //                        }
            //                        else
            //                        {
            //                            if (Logger.logger.FileTraceLevel == "Verbose") Logger.logger.Log(TraceEventType.Verbose, $"Deleting hidden file {file}.");
            //                            try
            //                            {
            //                                File.Delete(file);  //delete hidden files like .DS_Store
            //                            }
            //                            catch (Exception e)
            //                            {
            //                                Logger.logger.Log(TraceEventType.Critical, $"{e.Message} {e.StackTrace}");

            //                                if (e.InnerException != null)
            //                                {
            //                                    Logger.logger.Log(TraceEventType.Critical, $"Inner Exception: {e.InnerException}");
            //                                }
            //                            }

            //                        }

            //                    }
            //                }

            //                catch (Exception e)
            //                {
            //                    Logger.logger.Log(TraceEventType.Critical, $"{e.Message} {e.StackTrace}");
            //                    if (e.InnerException != null)
            //                    {
            //                        Logger.logger.Log(TraceEventType.Critical, $"Inner Exception: {e.InnerException}");
            //                    }
            //                }
            //            }


            //            _util.CleanUpDirectory(logPath);

            //        }
            //    }
            //}
            //catch (TaskCanceledException)
            //{
            //    Logger.logger.Log(TraceEventType.Information, $"Task was canceled.");
            //}
            //catch (Exception e)
            //{
            //    Logger.logger.Log(TraceEventType.Critical, $"{e.Message} {e.StackTrace}");
            //}
            //finally
            //{
            //    LITETask.Stop($"Purge");
            //}
        }
    }
}
