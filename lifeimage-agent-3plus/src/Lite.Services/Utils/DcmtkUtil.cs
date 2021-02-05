using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Lite.Core.Interfaces;
using Lite.Services;

namespace Lite.Core.Utils
{
    public sealed class DcmtkUtil : IDcmtkUtil
    {
        private readonly ILogger _logger;
        private readonly IFileProfileWriter _fileProfileWriter;
        private readonly IProfileWriter _profileWriter;
        private readonly ILITETask _taskManager;

        public DcmtkUtil(
            IFileProfileWriter fileProfileWriter,
            IProfileWriter profileWriter,
            ILITETask taskManager,
            ILogger<DcmtkUtil> logger)
        {
            _fileProfileWriter = fileProfileWriter;
            _profileWriter = profileWriter;
            _taskManager = taskManager;
            _logger = logger;
        }

        public void ExtractWindowsDCMTK()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"Platform is Windows");
                Console.WriteLine($"Windows: dcmtk-3.6.3-win64-dynamic.zip will be uncompressed to ../LITE/tools/dcmtk/dcmtk-3.6.3-win64-dynamic");
                ZipFile.ExtractToDirectory("./tools/dcmtk/dcmtk-3.6.3-win64-dynamic.zip", "tools/dcmtk");
            }
        }

        public bool ConfigureDcmtk(bool install, Profile profile, string currentProfile)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            profile.version++;

            //determine platform
            var platform = Environment.OSVersion.Platform;
            Console.WriteLine($"OSVersion is {Environment.OSVersion}");
            Console.WriteLine($"{RuntimeInformation.OSDescription}");
            Console.WriteLine($"Processor:{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} OS Arch:{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"Current Profile is {currentProfile}");


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine($"Platform is Linux");
                if (install)
                {
                    Console.WriteLine("Linux: dcmtk-3.6.3-linux-x86_64.tar.bz2 will be uncompressed ../LITE/tools/dcmtk/dcmtk-3.6.3-linux-x86_64");

                    //tar -xjvf tools/dcmtk/dcmtk-3.6.3-linux-x86_64.tar.bz2
                    var proc = new Process();
                    ProcessStartInfo procinfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        Verb = "runas",

                        //Directory.CreateDirectory("tools/dcmtk/dcmtk-3.6.3-linux-x86_64");
                        Arguments = $"-xjvf tools/dcmtk/dcmtk-3.6.3-linux-x86_64.tar.bz2 -C tools/dcmtk",
                        FileName = "tar"
                    };
                    proc.StartInfo = procinfo;


                    proc.Start();
                    proc.OutputDataReceived += (object sendingProcess,
                  DataReceivedEventArgs outLine) =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(outLine.Data))
                            {
                                Console.WriteLine($"{outLine.Data}");
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
                            if (!String.IsNullOrEmpty(outLine.Data))
                            {
                                Console.WriteLine($"{outLine.Data}");
                            }
                        }

                        catch (Exception e)
                        {
                            _logger.Log(LogLevel.Information, $"{e.Message}{e.StackTrace}");
                        }
                    };
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (object sender, EventArgs e) =>
                    {
                        Process p = (Process)sender;
                        if (p.ExitCode != 0)
                        {
                            Console.WriteLine($"{((Process)sender).StartInfo.FileName} Proc ExitCode:{proc.ExitCode}");
                        };
                    };

                    while (!proc.HasExited)
                    {
                        Console.WriteLine($"{procinfo.FileName} is running...");
                        Task.Delay(1000, _taskManager.cts.Token).Wait();
                    }

                    if (proc.ExitCode != 0)
                    {
                        Console.WriteLine($"Not updating dcmtkLibPath due to extraction error.");
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"Updating dcmtkLibPath.");
                        profile.dcmtkLibPath = "tools/dcmtk/dcmtk-3.6.3-linux-x86_64";
                        //profile.Save(currentProfile);
                        _fileProfileWriter.Save(profile, currentProfile);
                    }
                }

                else
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        Console.WriteLine($"Clearing dcmtkLibPath.");
                        Directory.Delete(profile.dcmtkLibPath, true);
                        profile.dcmtkLibPath = null;
                        //profile.Save(currentProfile);
                        _profileWriter.SaveProfile(profile).Wait();

                    }
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine($"Platform is OSX");
                if (install)
                {
                    Console.WriteLine("Mac: dcmtk-3.6.3-macosx-x86_64.tar.bz2 will be uncompressed to tools/dcmtk/dcmtk-3.6.3-macosx-x86_64");

                    //tar -xjvf tools/dcmtk/dcmtk-3.6.3-macosx-x86_64.tar.bz2
                    var proc = new Process();
                    ProcessStartInfo procinfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,

                        Verb = "runas",

                        //Directory.CreateDirectory("tools/dcmtk/dcmtk-3.6.3-macosx-x86_64");
                        Arguments = $"-xjvf tools/dcmtk/dcmtk-3.6.3-macosx-x86_64.tar.bz2 -C tools/dcmtk",
                        FileName = "tar"
                    };

                    proc.StartInfo = procinfo;                   
                    proc.OutputDataReceived += (object sendingProcess, DataReceivedEventArgs outLine) =>
                    {
                        try
                        {
                            if (!String.IsNullOrEmpty(outLine.Data))
                            {
                                Console.WriteLine($"{outLine.Data}");
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
                                Console.WriteLine($"{outLine.Data}");
                            }
                        }

                        catch (Exception e)
                        {
                            _logger.Log(LogLevel.Information, $"{e.Message}{e.StackTrace}");

                        }
                    };
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (object sender, EventArgs e) =>
                    {
                        Process p = (Process)sender;
                        if (p.ExitCode != 0)
                        {
                            Console.WriteLine($"{((Process)sender).StartInfo.FileName} Proc ExitCode:{proc.ExitCode}");
                        };
                    };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    while (!proc.HasExited)
                    {
                        Console.WriteLine($"{procinfo.FileName} is running...");
                        Task.Delay(1000, _taskManager.cts.Token).Wait();
                    }

                    if (proc.ExitCode != 0)
                    {
                        return false;
                    }
                    else
                    {
                        profile.dcmtkLibPath = "tools/dcmtk/dcmtk-3.6.3-maxosx-x86_64";
                        //profile.Save(currentProfile);
                        _fileProfileWriter.Save(profile, currentProfile);
                    }
                }
                else
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        Console.WriteLine($"Clearing dcmtkLibPath.");
                        Directory.Delete(profile.dcmtkLibPath, true);
                        profile.dcmtkLibPath = null;
                        //profile.Save(currentProfile);
                        _fileProfileWriter.Save(profile, currentProfile);

                    }
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"Platform is Windows");
                Console.WriteLine($"Windows: dcmtk-3.6.3-win64-dynamic.zip will be uncompressed to ../LITE/tools/dcmtk/dcmtk-3.6.3-win64-dynamic");

                if (install)
                {

                    ZipFile.ExtractToDirectory("tools/dcmtk/dcmtk-3.6.3-win64-dynamic.zip", "tools/dcmtk");
                    profile.dcmtkLibPath = "tools" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dcmtk-3.6.3-win64-dynamic";
                    //profile.Save(currentProfile);
                    _fileProfileWriter.Save(profile, currentProfile);

                }
                else
                {
                    if (profile.dcmtkLibPath != null)
                    {
                        Console.WriteLine($"Clearing dcmtkLibPath.");
                        Directory.Delete(profile.dcmtkLibPath, true);
                        profile.dcmtkLibPath = null;
                        //profile.Save(currentProfile);
                        _fileProfileWriter.Save(profile, currentProfile);
                    }
                }
            }

            return true;
        }
    }
}
