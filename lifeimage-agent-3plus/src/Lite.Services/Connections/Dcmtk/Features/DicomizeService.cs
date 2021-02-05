using Lite.Core.Connections;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IDicomizeService
    {
        Task<bool> Dicomize(int taskID, RoutedItem routedItem, DcmtkConnection connection);
    }
    public sealed class DicomizeService : DcmtkFeatureBase, IDicomizeService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IUtil _util;
        private readonly ILITETask _taskManager;

        public DicomizeService(
            IProfileStorage profileStorage,
            IUtil util,
            ILITETask taskManager,
            ILogger<DicomizeService> logger) 
            : base(logger)
        {
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _util = util;
        }

        public override DcmtkConnection Connection { get; set; }

        //try and determine type of file.  dcmtk can convert pdf, img, dump to dicom 
        public async Task<bool> Dicomize(int taskID, RoutedItem routedItem, DcmtkConnection connection)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var stopWatch = new Stopwatch();
            stopWatch.Start();


            var proc = new Process();
            var procinfo = new ProcessStartInfo();

            try
            {
                string args = $"{routedItem.sourceFileName} {routedItem.destFileName}";

                procinfo.UseShellExecute = false;
                procinfo.RedirectStandardError = true;
                procinfo.RedirectStandardOutput = true;
                procinfo.CreateNoWindow = true;
                procinfo.Arguments = args;
                proc.StartInfo = procinfo;

                proc.OutputDataReceived += OutputHandler;
                proc.ErrorDataReceived += ErrorHandler;
                proc.EnableRaisingEvents = true;
                proc.Exited += OnProcExit;

                var profile = _profileStorage.Current;

                if (routedItem.sourceFileName.EndsWith("pdf") || routedItem.sourceFileType == "pdf")
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "pdf2dcm";
                        var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                        procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                    }
                    else
                    {
                        procinfo.FileName = "pdf2dcm";
                    }
                }
                else if (routedItem.sourceFileName.EndsWith("xml") || routedItem.sourceFileType == "xml")
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "xml2dcm";
                        var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                        procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                    }
                    else
                    {
                        procinfo.FileName = "xml2dcm";
                    }
                }
                else if (routedItem.sourceFileName.EndsWith("jpg") || routedItem.sourceFileType == "jpg" || routedItem.sourceFileName.EndsWith("bmp") || routedItem.sourceFileType == "bmp")
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "img2dcm";
                        var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                        procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                    }
                    else
                    {
                        procinfo.FileName = "img2dcm";
                    }
                }
                else if (routedItem.sourceFileName.EndsWith("dump") || routedItem.sourceFileType == "dump")
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "dump2dcm";
                        var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                        DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                        procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                    }
                    else
                    {
                        procinfo.FileName = "dump2dcm";
                    }
                }

                _logger.Log(LogLevel.Information, $"{taskInfo} starting {procinfo.FileName} {procinfo.Arguments}");

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                //proc.WaitForExit();

                while (!proc.HasExited)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} is running...");
                    await Task.Delay(10000, _taskManager.cts.Token).ConfigureAwait(false);
                }

                if (proc.ExitCode != 0)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} {procinfo.FileName} ExitCode: {proc.ExitCode}");
                    return false;
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
                return false;
            }

            _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} Response id: {routedItem.id} status: {proc.ExitCode} elapsed: {stopWatch.Elapsed}");
            return true;
        }
    }
}
