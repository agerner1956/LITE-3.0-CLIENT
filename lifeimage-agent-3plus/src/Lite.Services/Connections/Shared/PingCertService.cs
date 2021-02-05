//using Dicom.Log;
using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public sealed class PingCertService
    {
        private readonly ILogger _logger;
        private readonly ILITETask _taskManager;

        public PingCertService(
            Connection connection, 
            ILITETask taskManager, 
            ILogger logger)
        {
            Throw.IfNull(connection);
            Connection = connection;
            _taskManager = taskManager;
            _logger = logger;
        }

        public Connection Connection { get; }

        public async Task<bool> PingCert(string URL, int taskID)
        {
            var taskInfo = $"conn: {Connection.name} task: {taskID}";
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            try
            {
                string args = null;

                if (URL != null && URL.StartsWith("https"))
                {
                    //Parse the URL to get host and port.
                    var builder = new UriBuilder(URL);
                    args = $"s_client -connect {builder.Host}:{builder.Port} -showcerts -prexit";
                }
                else if (Connection.remoteHostname != null && Connection.remotePort != 0 && Connection.useTLS)
                {
                    //openssl s_client -connect cloud.lifeimage.com:443 -showcerts -prexit
                    args = $"s_client -connect {Connection.remoteHostname}:{Connection.remotePort} -showcerts -prexit";
                }
                else
                {
                    _logger.Log(Microsoft.Extensions.Logging.LogLevel.Information,
                        $"{taskInfo} URL {URL} must begin with https, or remoteHostname {Connection.remoteHostname} and remotePort {Connection.remotePort} must be populated and useTLS {Connection.useTLS} set to true");
                    return false;
                }

                var proc = new Process();
                var procinfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    FileName = "openssl",
                    Arguments = args
                };
                proc.StartInfo = procinfo;

                proc.OutputDataReceived += OutputHandler;
                proc.ErrorDataReceived += ErrorHandler;
                proc.EnableRaisingEvents = true;
                proc.Exited += OnProcExit;
                _logger.Log(LogLevel.Information, $"{taskInfo} starting {procinfo.FileName} {args}");
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                var sr = proc.StandardInput;

                while (!proc.HasExited)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} openssl is running:  {Connection.name} {args}");
                    await Task.Delay(1000, _taskManager.cts.Token).ConfigureAwait(false);
                    if (!proc.HasExited)
                    {
                        sr.WriteLine("Q");
                    }

                    await Task.Delay(1000, _taskManager.cts.Token).ConfigureAwait(false);
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }

                if (proc.ExitCode != 0)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} {procinfo.FileName} ExitCode: {proc.ExitCode}");
                    return false;
                }

                _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} status: {proc.ExitCode.ToString()} elapsed: {stopWatch.Elapsed}");
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

            return true;
        }

        public void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            try
            {
                if (!string.IsNullOrEmpty(outLine.Data))
                {
                    _logger.Log(LogLevel.Information, $"{Connection.name}:{((Process)sendingProcess).StartInfo.FileName} {outLine.Data}");
                }
            }

            catch (Exception e)
            {
                _logger.Log(LogLevel.Information, $"{e.Message}{e.StackTrace}");
            }
        }

        public void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            try
            {
                if (!string.IsNullOrEmpty(outLine.Data))
                {
                    _logger.Log(LogLevel.Error,
                        $"{Connection.name}:{((Process)sendingProcess).StartInfo.FileName} {outLine.Data}");
                }
            }

            catch (Exception e)
            {
                _logger.Log(LogLevel.Information, $"{e.Message}{e.StackTrace}");
            }
        }

        public void OnProcExit(object sender, EventArgs e)
        {
            Process proc = (Process)sender;
            if (proc.ExitCode != 0)
            {
                _logger.Log(LogLevel.Warning, $"{Connection.name}:{((Process)sender).StartInfo.FileName} Proc ExitCode:{proc.ExitCode}");
                if (proc.ExitCode == -1)
                {
                    _logger.Log(LogLevel.Critical, $"Closing and Killing process {Connection.name}:{((Process)sender).StartInfo.FileName} Proc ExitCode:{proc.ExitCode}");
                    proc.Close();
                    proc.Kill();
                }
            }
        }
    }
}
