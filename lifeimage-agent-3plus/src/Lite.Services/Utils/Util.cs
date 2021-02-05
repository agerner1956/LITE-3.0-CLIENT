using Lite.Core.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Dicom;
using Lite.Services;

namespace Lite.Core.Utils
{
    public sealed class Util : IUtil
    {
        public const string programDataFolderName = "Life Image Transfer Exchange";

        private readonly IDiskUtils _diskUtils;
        private readonly ILogger _logger;

        public Util(
            IDiskUtils diskUtils,
            ILogger<Util> logger)
        {
            _diskUtils = diskUtils;
            _logger = logger;
        }

        public IDiskUtils DiskUtils => _diskUtils;

        public bool ConfigureService(bool install)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            //determine platform
            var platform = Environment.OSVersion.Platform;
            Console.WriteLine($"OSVersion is {Environment.OSVersion}");
            Console.WriteLine($"{RuntimeInformation.OSDescription}");
            Console.WriteLine($"Processor:{RuntimeInformation.ProcessArchitecture} OS Arch:{RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"{RuntimeInformation.FrameworkDescription}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine($"Platform is Linux");
                if (install)
                {
                    string service = $@"[Unit]
Description=LifeImageLite
After=network.target

[Service]
Type=simple
User={Environment.UserName}
WorkingDirectory={Directory.GetCurrentDirectory()}
ExecStart={Directory.GetCurrentDirectory()}/LITE
Restart=on-abort

[Install]
WantedBy=multi-user.target
";
                    //                    File.Copy("Setup/com.lifeimage.lite.service", "/etc/systemd/system/com.lifeimage.lite.service", true);
                    File.WriteAllText(service, "/etc/systemd/system/com.lifeimage.lite.service");
                }
                else
                {
                    File.Delete("/etc/systemd/system/com.lifeimage.lite.service");
                }
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine($"Platform is OSX");
                if (install)
                {
                    File.Copy("Setup/com.lifeimage.lite.plist", "/Library/LaunchDaemons/com.lifeimage.lite.plist", true);
                }
                else
                {
                    File.Delete("/Library/LaunchDaemons/com.lifeimage.lite.plist");
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"Platform is Windows");

                // Test with Current directory
                //string CurrentDirectory = "C:\\Program Files\\Life Image Transfer Exchange";
                ProcessStartInfo Info = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Verb = "runas",
                    FileName = "sc"
                };

                if (install)
                {

                    Info.Arguments = $"create \"LifeImage LITE\" binpath= \"\\\"{Environment.CurrentDirectory}\\LITE.exe\\\" action:run\" displayName= \"LifeImage LITE\" depend= \"LanmanServer\" start= \"auto\"";
                    CreateService(Info);
                    //Info.Arguments = $"description \"LifeImage LITE\" \"LifeImage LITE Release\"";
                    //CreateService(Info);
                    Info.Arguments = $"start \"LifeImage LITE\"";
                    CreateService(Info);
                }
                else
                {
                    Info.Arguments = $"delete \"LifeImage LITE\"";
                    CreateService(Info);
                }
            }

            return true;
        }

        public bool CreateService(ProcessStartInfo Info)
        {
            var proc = Process.Start(Info);
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                Console.WriteLine($"shell: {line}");
            }
            if (proc.ExitCode != 0)
            {
                return false;
            }
            return true;
        }

        public bool StartStopService(bool start)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            //determine platform
            var platform = Environment.OSVersion.Platform;
            Console.WriteLine($"OSVersion is {Environment.OSVersion}");
            Console.WriteLine($"{RuntimeInformation.OSDescription}");
            Console.WriteLine($"Processor:{RuntimeInformation.ProcessArchitecture} OS Arch:{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"{RuntimeInformation.FrameworkDescription}");

            ProcessStartInfo Info = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                Verb = "runas"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine($"Platform is Linux");

                Info.FileName = "service";

                if (start)
                {
                    Info.Arguments = "start com.lifeimage.lite.service";
                }
                else
                {
                    Info.Arguments = "stop com.lifeimage.lite.service";
                }
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine($"Platform is OSX");

                Info.FileName = "launchctl";

                if (start)
                {
                    Info.Arguments = "load -w /Library/LaunchDaemons/com.lifeimage.lite.plist";
                }
                else
                {
                    Info.Arguments = "unload /Library/LaunchDaemons/com.lifeimage.lite.plist";
                }
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"Platform is Windows");

                Info.FileName = "sc";

                if (start)
                {
                    Info.Arguments = $"start  \"LifeImage LITE\"";
                }
                else
                {
                    Info.Arguments = $"stop \"LifeImage LITE\"";
                }
            }

            var proc = Process.Start(Info);

            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                // do something with line
                Console.WriteLine($"shell: {line}");
            }

            if (proc.ExitCode != 0) return false;

            return true;
        }

        public string GetTempFolder(string path = "")
        {
            string retval;

            _logger.Log(LogLevel.Debug, $"path: {path} fullPath: {(path == "" ? "" : Path.GetFullPath(path))} fullyQualified: {Path.IsPathFullyQualified(path)} rooted: {Path.IsPathRooted(path)} ProfileDirInProgramPath: {System.IO.Directory.Exists("Profile")}");

            if (path != "" && Path.GetFullPath(path) == path)
            {
                _logger.Log(LogLevel.Debug, $"Path and FullPath are the same, returning: {path}");

                // In case they provide a full path name
                return path;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == true && !System.IO.Directory.Exists("Profiles"))
            {
                _logger.Log(LogLevel.Debug, $"Windows MSI Magic");

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                _logger.Log(LogLevel.Debug, $"Environment.SpecialFolder.CommonApplicationData: {Environment.SpecialFolder.CommonApplicationData} appData: {appData}");
                string BaseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                _logger.Log(LogLevel.Debug, $"System.Reflection.Assembly.GetExecutingAssembly().Location: {System.Reflection.Assembly.GetExecutingAssembly().Location} baseDir: {BaseDir}");

                if (path == "")
                {
                    retval = Path.GetFullPath(appData + Path.DirectorySeparatorChar + programDataFolderName);
                }
                else
                {

                    retval = Path.GetFullPath(appData + Path.DirectorySeparatorChar + programDataFolderName + Path.DirectorySeparatorChar + path);

                }

                _logger.Log(LogLevel.Debug, $"returning magic path: {retval}");

                return retval;
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (home == null)
                {
                    home = "";
                }
                if (path == "")
                {
                    retval = (home == "" ? "" : home + Path.DirectorySeparatorChar) + programDataFolderName + Path.DirectorySeparatorChar + LiteEngine.version + Path.DirectorySeparatorChar + Directory.GetCurrentDirectory().Substring(Directory.GetCurrentDirectory().LastIndexOf(Path.DirectorySeparatorChar) + 1);
                    _logger.Log(LogLevel.Debug, $"home: {home} returning home path: {retval}");
                    return retval;

                }
                else
                {
                    retval = (home == "" ? "" : home + Path.DirectorySeparatorChar) + programDataFolderName + Path.DirectorySeparatorChar + LiteEngine.version + Path.DirectorySeparatorChar + Directory.GetCurrentDirectory().Substring(Directory.GetCurrentDirectory().LastIndexOf(Path.DirectorySeparatorChar) + 1) + Path.DirectorySeparatorChar + path;
                    _logger.Log(LogLevel.Debug, $"home: {home} returning combined home path: {retval}");
                    return retval;
                }
            }
        }

        public string EnvSeparatorChar()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ";";
            }
            else
            {
                return ":";
            }
        }

        public Priority GetPriority(ushort priority)
        {
            switch (priority)
            {
                case 0x00: //medium
                    return Priority.Medium;
                case 0x01: //high
                    return Priority.High;
                case 0x02: //low
                default:
                    return Priority.Low;
            }
        }

        public ushort GetPriority(Priority priority)
        {
            switch (priority)
            {
                case Priority.Medium: //medium
                    return 0x00;
                case Priority.High: //high
                    return 0x01;
                case Priority.Low: //low
                default:
                    return 0x02;
            }
        }

        // https://stackoverflow.com/questions/25366534/file-writealltext-not-flushing-data-to-disk
        public void WriteAllTextWithBackup(string path, string contents)
        {
            _logger.Log(LogLevel.Debug, $"path: {path}");
            _logger.Log(LogLevel.Debug, $"Parsing path with DirectorySeparatorChar: {Path.DirectorySeparatorChar}");

            var index = path.LastIndexOf(Path.DirectorySeparatorChar);
            if (index == -1)
            {
                _logger.Log(LogLevel.Debug, $"Parsing path with AltDirectorySeparatorChar: {Path.AltDirectorySeparatorChar}");
                index = path.LastIndexOf(Path.AltDirectorySeparatorChar);
            }
            if (index == -1)
            {
                _logger.Log(LogLevel.Debug, $"Parsing path with /");
                index = path.LastIndexOf("/");
            }
            if (index != -1)
            {
                string dir = path.Substring(0, index);

                _logger.Log(LogLevel.Debug, $"dir: {dir}");

                Directory.CreateDirectory(dir);
            }
            else
            {
                _logger.Log(LogLevel.Debug, $"Unable to parse path {path} to create dir.");
            }

            // generate a temp filename
            var tempPath = Path.GetTempFileName();

            _logger.Log(LogLevel.Debug, $"tempPath: {tempPath}");
            _logger.Log(LogLevel.Debug, $"path: {path}");

            // create the backup name
            var backup = path + ".backup";

            _logger.Log(LogLevel.Debug, $"backup: {backup}");

            // delete any existing backups
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            // get the bytes
            var data = Encoding.UTF8.GetBytes(contents);

            // write the data to a temp file
            using (var tempFile = File.Create(tempPath, 4096, FileOptions.WriteThrough))
            {
                tempFile.Write(data, 0, data.Length);
            }

            // replace the contents
            File.Replace(tempPath, path, backup);
        }

        public void EncapsulatePDF(string file, string outFile)
        {
            DicomDataset data = new DicomDataset
            {
                { DicomTag.SOPClassUID, DicomUID.EncapsulatedPDFStorage }
            };
            byte[] fileData = DiskUtils.ReadBytesFromFile(file);
            data.Add(DicomTag.EncapsulatedDocument, fileData);
            DicomFile ff = new DicomFile(data);
            ff.Save(@outFile);
        }

        public string RemoveInvalidPathAndFileCharacters(string inputString)
        {
            if (inputString == null)
            {
                _logger.Log(LogLevel.Debug, $"Replacing null inputString with empty string");
                inputString = "";
            }

            //replace invalid filename characters.  This is not complete and can vary by file system.
            // Get a list of invalid path characters.
            char[] invalidPathChars = Path.GetInvalidPathChars();

            foreach (var c in invalidPathChars)
            {
                if (inputString.Contains(c))
                {
                    _logger.Log(LogLevel.Debug, $"Replacing '{c}' with '-' in {inputString}");
                    inputString = inputString.Replace(c, '-');
                }
            }

            // Get a list of invalid file characters.
            char[] invalidFileChars = Path.GetInvalidFileNameChars();


            foreach (var c in invalidFileChars)
            {
                if (inputString.Contains(c))
                {
                    _logger.Log(LogLevel.Debug, $"Replacing '{c}' with '-' in {inputString}");
                    inputString = inputString.Replace(c, '-');
                }
            }

            //simple check if above doesn't work
            if (inputString.Contains(':'))
            {
                _logger.Log(LogLevel.Debug, $"Replacing ':' with '-' in {inputString}");
                inputString = inputString.Replace(':', '-');
            }

            return inputString;
        }
    }
}
