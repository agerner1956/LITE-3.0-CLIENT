using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
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
    public interface IDcmtkDumpService
    {
        Task<RoutedItem> DcmDump(int taskID, RoutedItem routedItem, DcmtkConnection Connection);
    }

    public sealed class DcmtkDumpService : IDcmtkDumpService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILogger _logger;
        private readonly IUtil _util;
        private readonly IRoutedItemManager _routedItemManager;
        public DcmtkDumpService(
            IProfileStorage profileStorage,
            IRoutedItemManager routedItemManager,
            IUtil util,
            ILogger<DcmtkScanner> logger)
        {
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _util = util;
            _logger = logger;
        }

        public DcmtkConnection Connection { get; set; }

        public async Task<RoutedItem> DcmDump(int taskID, RoutedItem routedItem, DcmtkConnection connection)
        {
            Connection = connection;

            /* 
                EX:  dcmdump +P "0008,0050" +P "0010,0010" ~/Public/00011001.dcm           
                Returns:
                    (0008,0050) SH [3972022]                                #   8, 1 AccessionNumber    
                    (0010,0010) PN [HADDAD WILLIE]                          #  14, 1 PatientName
            */

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            Dictionary<string, string> args = new Dictionary<string, string>();

            var proc = new Process();
            var procinfo = new ProcessStartInfo();

            try
            {
                //default return tags
                args.TryAdd("0008,0050", "+P \"0008,0050\""); //AccessionNumber
                args.TryAdd("0008,0060", "+P \"0008,0060\""); //Modality
                args.TryAdd("0010,0020", "+P \"0010,0020\""); //PatientID
                args.TryAdd("0020,0010", "+P \"0020,0010\""); //Study ID
                args.TryAdd("0020,000d", "+P \"0020,000d\""); //Study Instance UID

                args.TryAdd("file", $"\"{routedItem.sourceFileName}\"");

                //connect the cFind to the RoutedItem that originated the request
                //the cache was already primed in GetRequests
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();
                routedItem.status = RoutedItem.Status.PENDING;
                procinfo.UseShellExecute = false;
                procinfo.RedirectStandardError = true;
                procinfo.RedirectStandardOutput = true;
                procinfo.CreateNoWindow = true;

                var profile = _profileStorage.Current;

                if (profile.dcmtkLibPath != null)
                {
                    procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "dcmdump";
                    var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                    procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                }
                else
                {
                    procinfo.FileName = "dcmdump";
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
                proc.Exited += OnDcmDumpExit;

                _logger.Log(LogLevel.Information, $"{taskInfo} starting {procinfo.FileName} {procinfo.Arguments}");

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                // while (!proc.HasExited)
                // {
                //     //_logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} is running...");
                //     await Task.Delay(1000, LITETask.cts.Token).ConfigureAwait(false);
                // }

                if (proc.ExitCode != 0)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} {procinfo.FileName} ExitCode: {proc.ExitCode}");
                    return routedItem;
                }

                _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} Response id: {routedItem.id} status: {proc.ExitCode.ToString()} elapsed: {stopWatch.Elapsed}");

                if (response != null)
                {
                    // scrape the stdout/stderr responses for the answers
                    foreach (var responseItem in response)
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} Response id: {routedItem.id}, {responseItem}");
                        if (responseItem != null)
                        {
                            if (responseItem.Length >= 12 && !responseItem.StartsWith("W:"))
                            {
                                if (responseItem.StartsWith("I: ---------------------------")) { }

                                var test1 = responseItem.Substring(1, 9);
                                var result = args.Keys.Any(e => e.Contains(test1));
                                if (args.Keys.Any(e => e.Contains(responseItem.Substring(1, 9))))
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
                                    var success = routedItem.TagData.TryAdd(responseItem.Substring(1, 9), data);
                                    if (!success)
                                    {
                                        //concatenate multiple tags if present
                                        success = routedItem.TagData[responseItem.Substring(1, 9)] == routedItem.TagData[responseItem.Substring(1, 9)] + "+" + data;
                                    }
                                }
                            }
                        }
                    }
                }

                routedItem.TagData.TryGetValue("0010,0020", out routedItem.PatientID);
                routedItem.TagData.TryGetValue("0008,0050", out routedItem.AccessionNumber);
                routedItem.TagData.TryGetValue("0020,000d", out routedItem.Study);  //studyinstanceuid
                routedItem.TagData.TryGetValue("0020,0010", out routedItem.StudyID);
                //routedItem.TagData.TryGetValue("0008,0060", out routedItem.Modality);
                //construct an id with the standard fields
                routedItem.id = $"PID:{routedItem.PatientID}, AN:{routedItem.AccessionNumber}"; //, UID:{routedItem.Study}";
                                                                                                //var md = new ModalityDetection();
                                                                                                // md.ModalityFiltering(routedItem, ModalityList);
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
                    _routedItemManager.Dequeue(Connection, Connection.toDicom, nameof(Connection.toDicom), true);
                }
                routedItem.fromConnection = Connection.name;
                routedItem.toConnections.Clear();
                routedItem.type = RoutedItem.Type.DICOM;

                _routedItemManager.Init(routedItem);
                _routedItemManager.Enqueue(Connection, Connection.toRules, nameof(Connection.toRules));
                return routedItem;
            }

            return routedItem;
        }

        public void OnDcmDumpExit(object sender, EventArgs e)
        {
            Process proc = (Process)sender;
            if (proc.ExitCode != 0)
            {
                _logger.Log(LogLevel.Warning, $"{Connection.name}:{((Process)sender).StartInfo.FileName} Proc ExitCode:{proc.ExitCode}");
            }
        }
    }
}
