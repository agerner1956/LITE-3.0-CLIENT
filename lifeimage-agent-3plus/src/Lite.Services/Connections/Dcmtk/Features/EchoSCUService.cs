using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IEchoSCUService
    {
        Task<bool> EchoSCU(int taskID, DcmtkConnection connection);
    }

    public sealed class EchoSCUService : DcmtkFeatureBase, IEchoSCUService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly IUtil _util;

        public EchoSCUService(
            IProfileStorage profileStorage,
            IUtil util,
            ILITETask taskManager,
            ILogger<EchoSCUService> logger) : base(logger)
        {
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _util = util;
        }

        public override DcmtkConnection Connection { get; set; }

        public async Task<bool> EchoSCU(int taskID, DcmtkConnection connection)
        {
            Connection = connection;

            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var proc = new Process();
            var procinfo = new ProcessStartInfo();

            var profile = _profileStorage.Current;

            try
            {
                string args = $"{Connection.remoteHostname} {Connection.remotePort} -aec {Connection.remoteAETitle} -aet {Connection.localAETitle}";

                procinfo.UseShellExecute = false;
                procinfo.RedirectStandardError = true;
                procinfo.RedirectStandardOutput = true;
                procinfo.CreateNoWindow = true;
                if (profile.dcmtkLibPath != null)
                {
                    procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "echoscu";
                    var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + Constants.Dirs.share + Path.DirectorySeparatorChar + Constants.Dirs.dcmtk + Path.DirectorySeparatorChar + "dicom.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + Constants.Dirs.share + Path.DirectorySeparatorChar + Constants.Dirs.dcmtk + Path.DirectorySeparatorChar + "acrnema.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + Constants.Dirs.share + Path.DirectorySeparatorChar + Constants.Dirs.dcmtk + Path.DirectorySeparatorChar + "diconde.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + Constants.Dirs.share + Path.DirectorySeparatorChar + Constants.Dirs.dcmtk + Path.DirectorySeparatorChar + "private.dic";
                    procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                }
                else
                {
                    procinfo.FileName = "echoscu";
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
                    _logger.Log(LogLevel.Information, $"{taskInfo} echoscu is running...");
                    await Task.Delay(1000, _taskManager.cts.Token).ConfigureAwait(false);
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
                _logger.Log(LogLevel.Critical, $"{e.Message} {e.StackTrace}");
                return false;
            }
            finally
            {
                _taskManager.Stop($"{Connection.name}.echoscu");
            }

            _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} status: {proc.ExitCode.ToString()} elapsed: {stopWatch.Elapsed}");
            return true;
        }
    }
}
