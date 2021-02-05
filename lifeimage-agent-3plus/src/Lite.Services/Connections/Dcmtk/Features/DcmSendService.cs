using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IDcmSendService
    {
        Task<bool> DcmSend(int taskID, DcmtkConnection connection);
    }

    public class DcmSendService : DcmtkFeatureBase, IDcmSendService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly IUtil _util;
        public DcmSendService(
            IProfileStorage profileStorage,
            ILITETask taskManager,
            IUtil util,
            ILogger<DcmSendService> logger) : base(logger)
        {            
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _util = util;
        }

        public override DcmtkConnection Connection { get; set; }

        public async Task<bool> DcmSend(int taskID, DcmtkConnection connection)
        {
            Throw.IfNull(connection);

            this.Connection = connection;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var profile = _profileStorage.Current;
            bool scan = true, recurse = false, nohalt = true;
            string path = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toDcmsend";

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var proc = new Process();
            var procinfo = new ProcessStartInfo();

            try
            {
                Directory.CreateDirectory(path);
                string args = $"{Connection.remoteHostname} {Connection.remotePort} \"{path}\" {(scan ? "+sd" : "")} {(recurse ? "+r" : "")} {(nohalt ? "-nh" : "")} -aec {Connection.remoteAETitle} -aet {Connection.localAETitle}";
                procinfo.UseShellExecute = false;
                procinfo.RedirectStandardError = true;
                procinfo.RedirectStandardOutput = true;
                procinfo.CreateNoWindow = true;
                if (profile.dcmtkLibPath != null)
                {
                    procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "dcmsend";
                    var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                    procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                }
                else
                {
                    procinfo.FileName = "dcmsend";
                }
                procinfo.Arguments = args;
                proc.StartInfo = procinfo;

                proc.OutputDataReceived += OutputHandler;
                proc.ErrorDataReceived += ErrorHandler;
                proc.EnableRaisingEvents = true;
                proc.Exited += OnProcExit;

                _logger.Log(LogLevel.Information, $"{taskInfo} starting {procinfo.FileName} {procinfo.Arguments}");

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                //proc.WaitForExit();

                while (!proc.HasExited)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} dcmsend is running...");
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

            _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} status: {proc.ExitCode.ToString()} elapsed: {stopWatch.Elapsed}");

            return true;
        }
    }
}
