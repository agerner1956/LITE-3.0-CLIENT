using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections.Dicom.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IFindSCUService
    {
        Task<RoutedItem> FindSCU(int taskID, RoutedItem routedItem, DcmtkConnection connection);
    }

    public sealed class FindSCUService : DcmtkFeatureBase, IFindSCUService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IUtil _util;
        private readonly IRoutedItemManager _routedItemManager;        
        private readonly ILITETask _taskManager;

        public FindSCUService(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IUtil util,            
            ILITETask taskManager,
            ILogger<FindSCUService> logger) : base(logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _util = util;
        }

        public override DcmtkConnection Connection { get; set; }
        public async Task<RoutedItem> FindSCU(int taskID, RoutedItem routedItem, DcmtkConnection connection)
        {
            Connection = connection;
            //EX:  findscu  -P -k "(0008,0052)=SERIES" -k "(0010,0010)" -k "(0010,0020)" -k "(0010,0020)"  -k "(0008,0050)" -k "(0020,000D)" -k "(0008,0005)" -k "(0020,1208)"  -aec DCMTK  localhost 11120

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            Dictionary<string, string> args = new Dictionary<string, string>();

            var proc = new Process();
            var procinfo = new ProcessStartInfo();

            try
            {
                LiCloudRequest cFindParams = LiCloudRequest.FromJson(routedItem.request, _logger);
                foreach (var tag in cFindParams.searchTags)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} id: {routedItem.id} tag: {tag.Key} {tag.Value}");
                }

                cFindParams.searchTags.TryGetValue("0008,0052", out string cfindlevel);

                switch (cfindlevel)
                {
                    case "PATIENT":
                        args.TryAdd("level", "-P");
                        break;
                    case "WORKLIST":
                        args.TryAdd("level", "-W");
                        break;
                    case "STUDY":
                    default:
                        args.TryAdd("level", "-S");
                        args.TryAdd("0008,0052", "-k \"(0008,0052)=STUDY\"");

                        break;
                }

                //MessageID: {cFind.MessageID}
                _logger.Log(LogLevel.Information, $"{taskInfo} Request id: {routedItem.id}  attempt: {routedItem.attempts}");

                //default return tags
                args.TryAdd("0008,0005", "-k \"(0008,0005)\""); //Specific Character Set
                args.TryAdd("0008,0050", "-k \"(0008,0050)\""); //AccessionNumber
                args.TryAdd("0008,0054", "-k \"(0008,0054)\""); //Retrieve AE Title 
                args.TryAdd("0020,1208", "-k \"(0020,1208)\""); //NumberOfStudyRelatedInstances
                args.TryAdd("0020,1209", "-k \"(0020,1209)\""); //NumberOfSeriesRelatedInstances
                args.TryAdd("0008,0060", "-k \"(0008,0060)\""); //Modality
                args.TryAdd("0008,0061", "-k \"(0008,0061)\""); //ModalitiesInStudy
                args.TryAdd("0010,0020", "-k \"(0010,0020)\""); //PatientID
                args.TryAdd("0010,0010", "-k \"(0010,0010)\""); //PatientName
                args.TryAdd("0010,0030", "-k \"(0010,0030)\""); //PatientBirthDate
                args.TryAdd("0010,0040", "-k \"(0010,0040)\""); //PatientSex
                args.TryAdd("0008,0020", "-k \"(0008,0020)\""); //StudyDate
                args.TryAdd("0020,0010", "-k \"(0020,0010)\""); //StudyID
                args.TryAdd("0020,000d", "-k \"(0020,000D)\""); //StudyInstanceUID
                args.TryAdd("0008,1030", "-k \"(0008,1030)\""); //StudyDescription

                // add the search tags
                foreach (KeyValuePair<string, string> tag in cFindParams.searchTags)
                {
                    try
                    {
                        var exists = args.TryGetValue(tag.Key, out string arg);
                        var newArg = $"-k \"({tag.Key})={tag.Value}\"";

                        if (exists)
                        {
                            args[tag.Key] = newArg;
                        }
                        else
                        {
                            args[tag.Key] = newArg;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e, taskInfo);
                    }
                }

                args.TryAdd("host", $"-aec {Connection.remoteAETitle} -aet {Connection.localAETitle} {Connection.remoteHostname} {Connection.remotePort}");

                var profile = _profileStorage.Current;
                //connect the cFind to the RoutedItem that originated the request
                //the cache was already primed in GetRequests
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();
                routedItem.status = RoutedItem.Status.PENDING;
                procinfo.UseShellExecute = false;
                procinfo.RedirectStandardError = true;
                procinfo.RedirectStandardOutput = true;
                procinfo.CreateNoWindow = true;
                if (profile.dcmtkLibPath != null)
                {
                    procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "findscu";
                    var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                    procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                }
                else
                {
                    procinfo.FileName = "findscu";
                }
                procinfo.Arguments = string.Join(" ", args.Values.ToArray()); ;
                proc.StartInfo = procinfo;
                List<string> response = new List<string>();
                proc.OutputDataReceived += (object sendingProcess, DataReceivedEventArgs outLine) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(outLine.Data))
                        {
                            response.Add(outLine.Data);
                        }
                    }

                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Information, $"{e.Message}{e.StackTrace}");

                    }
                };
                proc.ErrorDataReceived += (object sendingProcess, DataReceivedEventArgs outLine) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(outLine.Data))
                        {
                            response.Add(outLine.Data);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Information, $"{e.Message}{e.StackTrace}");

                    }
                };
                proc.EnableRaisingEvents = true;
                proc.Exited += OnProcExit;

                _logger.Log(LogLevel.Information, $"{taskInfo} starting {procinfo.FileName} {procinfo.Arguments}");

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                //proc.WaitForExit();

                while (!proc.HasExited)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} is running...");
                    await Task.Delay(1000, _taskManager.cts.Token).ConfigureAwait(false);
                }

                // if (proc.ExitCode != 0)
                // {
                //     _logger.Log(LogLevel.Warning, $"{taskInfo} {procinfo.FileName} ExitCode: {proc.ExitCode}");
                //     return false;
                // }

                _logger.Log(LogLevel.Information, $"{taskInfo} Response id: {routedItem.id} status: {proc.ExitCode.ToString()} elapsed: {stopWatch.Elapsed}");

                Dictionary<string, string> returnTagData = new Dictionary<string, string>();
                string jsonResults = null;
                string key = "response";
                Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>();
                if (response != null)
                {
                    // scrape the stdout/stderr responses for the answers
                    int i = 0;
                    foreach (var responseItem in response)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Response id: {routedItem.id}, {responseItem}");
                        if (responseItem != null)
                        {
                            returnTagData.Add($"raw{++i}", responseItem);
                            if (responseItem.Length >= 12)
                            {
                                if (responseItem.StartsWith("I: ---------------------------")) { }

                                var test1 = responseItem.Substring(4, 9);
                                var result = args.Keys.Any(e => e.Contains(test1));

                                if (args.Keys.Any(e => e.Contains(responseItem.Substring(4, 9))))
                                {
                                    string data = null;

                                    if (responseItem.Contains("no value available"))
                                    {
                                        data = "";
                                    }
                                    else
                                    {
                                        data = responseItem.Substring(responseItem.IndexOf("[") + 1, responseItem.LastIndexOf("]") - responseItem.IndexOf("[") - 1).Trim().Trim('\0');
                                    }
                                    if (responseItem.Substring(3, 11) == "(0008,0054)") //BOUR-940 mask AETitle with conn name
                                    {
                                        returnTagData.Add(responseItem.Substring(3, 11), Connection.name);
                                    }
                                    else
                                    {
                                        returnTagData.Add(responseItem.Substring(3, 11), data);
                                    }
                                }
                                if (responseItem.Contains("0020,000d"))//StudyInstanceUID
                                {
                                    key = responseItem.Substring(responseItem.IndexOf("[") + 1, responseItem.LastIndexOf("]") - responseItem.IndexOf("[") - 1).Trim().Trim('\0');
                                }
                            }
                            else
                            {

                                if (responseItem.Equals("I: ") && key != "response")
                                {
                                    if (proc.ExitCode != 0)
                                    {
                                        returnTagData.Add("StatusCode", proc.ExitCode.ToString());
                                    }
                                    results.Add(key, new Dictionary<string, string>(returnTagData));
                                    jsonResults = JsonSerializer.Serialize(results);
                                    routedItem.response.Add(jsonResults);
                                    results.Clear();
                                    returnTagData.Clear();
                                    key = "response";
                                }
                            }
                        }
                    }
                }

                if (routedItem.response.Count == 0)
                {
                    //per Victoria she doesn't want status code if there is no result
                    if (proc.ExitCode != 0)
                    {
                        returnTagData.Add("StatusCode", proc.ExitCode.ToString());
                    }
                    results.Add(key, returnTagData);
                    jsonResults = JsonSerializer.Serialize(results);
                    // set results in RoutedItem
                    routedItem.response.Add(jsonResults);
                }

                RoutedItem toCache = null;

                switch (proc.ExitCode)
                {
                    case 0:
                        routedItem.status = RoutedItem.Status.COMPLETED;
                        routedItem.resultsTime = DateTime.Now;

                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toFindSCU, nameof(Connection.toFindSCU), error: false);
                        toCache = (RoutedItem)_routedItemManager.Clone();
                        toCache.fromConnection = Connection.name;
                        toCache.toConnections.Clear(); //BOUR-863 the toConnections on the toCache object weren't being cleared before rules so it contained DICOMConnection which
                        toCache.attempts = 0;
                        toCache.lastAttempt = DateTime.MinValue;
                        toCache.type = RoutedItem.Type.RPC;

                        _routedItemManager.Init(toCache);
                        _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
                        break;

                    default:

                        routedItem.status = RoutedItem.Status.FAILED;
                        routedItem.resultsTime = DateTime.Now;
                        _routedItemManager.Init(routedItem);
                        _routedItemManager.Dequeue(Connection, Connection.toFindSCU, nameof(Connection.toFindSCU), error: true);
                        toCache = (RoutedItem)_routedItemManager.Clone();
                        toCache.fromConnection = Connection.name;
                        toCache.toConnections.Clear(); //BOUR-863 the toConnections on the toCache object weren't being cleared before rules so it contained DICOMConnection which
                        toCache.attempts = 0;
                        toCache.lastAttempt = DateTime.MinValue;
                        toCache.type = RoutedItem.Type.RPC;

                        _routedItemManager.Init(toCache);
                        _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                        break;
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);

                Dictionary<string, string> returnTagData = new Dictionary<string, string>
                {
                    { "StatusCode", "-1" },
                    { "StatusDescription", $"Error: {e.Message}" },
                    { "StatusErrorComment", $"Error: {e.StackTrace}" }
                };

                string key = "response";

                Dictionary<string, Dictionary<string, string>> results = new Dictionary<string, Dictionary<string, string>>
                {
                    { key, returnTagData }
                };
                string jsonResults = JsonSerializer.Serialize(results);
                routedItem.response.Add(jsonResults);
                if (routedItem.attempts > Connection.maxAttempts)
                {
                    routedItem.status = RoutedItem.Status.FAILED;
                    routedItem.resultsTime = DateTime.Now;
                    _logger.Log(LogLevel.Debug, $"{taskInfo} id: {routedItem.id} exceeded max attempts.");

                    _routedItemManager.Init(routedItem);
                    _routedItemManager.Dequeue(Connection, Connection.toFindSCU, nameof(Connection.toFindSCU), true);
                }
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();
                routedItem.type = RoutedItem.Type.RPC;

                _routedItemManager.Init(routedItem);
                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));

                return routedItem;
            }

            return routedItem;
        }
    }
}
