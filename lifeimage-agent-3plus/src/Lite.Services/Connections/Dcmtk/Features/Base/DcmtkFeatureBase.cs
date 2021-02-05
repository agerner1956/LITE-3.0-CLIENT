using Lite.Core.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public abstract class DcmtkFeatureBase
    {
        protected readonly ILogger _logger;

        protected DcmtkFeatureBase(ILogger logger)
        {
            _logger = logger;
        }

        public abstract DcmtkConnection Connection { get; set; }

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
                    _logger.Log(LogLevel.Error, $"{Connection.name}:{((Process)sendingProcess).StartInfo.FileName} {outLine.Data}");
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
                    _logger.Log(LogLevel.Critical, $"Not Closing and Killing process {Connection.name}:{((Process)sender).StartInfo.FileName} Proc ExitCode:{proc.ExitCode}");
                    //proc.Close();
                    //proc.Kill();
                }
            }
        }
    }
}
