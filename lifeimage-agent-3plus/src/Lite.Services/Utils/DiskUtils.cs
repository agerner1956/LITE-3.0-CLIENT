using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Lite.Services;
using Microsoft.Extensions.Logging;

namespace Lite.Core.Utils
{
    public sealed class DiskUtils : IDiskUtils
    {
        private readonly ILogger _logger;

        public DiskUtils(ILogger<DiskUtils> logger)
        {
            _logger = logger;
        }

        public void CleanUpDirectory(string startLocation, int retentionMinutes = 5)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                var fileInfo = new FileInfo(directory);
                var purgetime = DateTime.Now.AddMinutes(retentionMinutes * -1);

                CleanUpDirectory(directory);
                if (!directory.EndsWith("toScanner")
                    && Directory.GetFiles(directory).Length == 0
                    && Directory.GetDirectories(directory).Length == 0
                    //&& fileInfo.LastAccessTime.CompareTo(purgetime) < 0
                    && fileInfo.CreationTime.CompareTo(purgetime) < 0
                    && fileInfo.LastWriteTime.CompareTo(purgetime) < 0)
                {
                    Directory.Delete(directory, false);
                    _logger.Log(LogLevel.Debug, $"deleted empty: {directory} Creation: {fileInfo.CreationTime} LastWrite: {fileInfo.LastWriteTime} LastAccess: {fileInfo.LastAccessTime}");
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"not deleting empty: {directory} Creation: {fileInfo.CreationTime} LastWrite: {fileInfo.LastWriteTime} LastAccess: {fileInfo.LastAccessTime}");
                }
            }
        }

        public List<string> DirSearch(string sDir, string pattern)
        {
            List<string> retval = new List<string>();

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            try
            {
                foreach (string f in Directory.GetFiles(sDir, pattern))
                {
                    retval.Add(f);
                }

                foreach (string d in Directory.GetDirectories(sDir))
                {
                    retval.AddRange(DirSearch(d, pattern));
                }
            }
            catch (System.Exception e)
            {
                _logger.LogFullException(e);
            }

            _logger.Log(LogLevel.Warning, $"dir: {sDir} pattern: {pattern} items: {retval.Count} elapsed: {stopWatch.Elapsed}");
            return retval;
        }

        public byte[] ReadBytesFromFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                FileStream fs = File.OpenRead(fileName);
                try
                {
                    byte[] bytes = new byte[fs.Length];
                    fs.Read(bytes, 0, Convert.ToInt32(fs.Length));
                    fs.Close();
                    return bytes;
                }
                finally
                {
                    fs.Close();
                }
            }
            else
            {
                return new byte[0];
            }
        }

        /// <summary>
        /// checks the specific drive in the path for available space.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="profile"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public bool IsDiskAvailable(string path, Profile profile, long length = 0)
        {
            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                List<string> insufficientDrives = new List<string>();

                //get the drive off the path
                var fileinfo = new FileInfo(path);
                var fullname = fileinfo.FullName;
                var drive = Path.GetPathRoot(fullname);

                if (length == 0 && fileinfo.Exists)
                {
                    length = fileinfo.Length;
                }

                foreach (DriveInfo d in allDrives)
                {
                    if (drive == d.Name && d.IsReady == true && d.DriveType.Equals(DriveType.Fixed) &&
                        !d.VolumeLabel.Equals("System Reserved"))
                    {
                        if (profile != null)
                        {
                            _logger.Log(LogLevel.Debug, $"Drive {d.Name} type: {d.DriveType} label: {d.VolumeLabel} format: {d.DriveFormat} user: {d.AvailableFreeSpace} total: {d.TotalFreeSpace} min: {profile.minFreeDiskBytes}");

                            if ((d.AvailableFreeSpace - length) < profile.minFreeDiskBytes)
                            {
                                _logger.Log(LogLevel.Warning, $"Insufficient Free Disk {d.AvailableFreeSpace} minus minFreeDiskBytes {profile.minFreeDiskBytes} = {d.AvailableFreeSpace - profile.minFreeDiskBytes} Available on {d.Name} to write file of size {length}");
                                insufficientDrives.Add(d.Name);
                            }
                        }
                    }
                }

                if (insufficientDrives.Count > 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }

            return true;
        }

        public void MoveFileToErrorFolder(string tempPath, string filePath, string name = "")
        {
            try
            {
                DateTime now = DateTime.Now;
                var errorFile = tempPath + Path.DirectorySeparatorChar + Constants.Dirs.Errors + Path.DirectorySeparatorChar + now.Year + Path.DirectorySeparatorChar + now.Month + Path.DirectorySeparatorChar + now.Day + Path.DirectorySeparatorChar + now.Hour + Path.DirectorySeparatorChar +
                                name + filePath.Substring(filePath.LastIndexOf(Path.DirectorySeparatorChar));

                Directory.CreateDirectory(errorFile.Substring(0, errorFile.LastIndexOf(Path.DirectorySeparatorChar)));

                if (File.Exists(errorFile))
                {
                    var orgErrorFileDateTime = File.GetLastWriteTime(errorFile).ToString().Replace(Path.DirectorySeparatorChar, '-').Replace(":", "-");
                    var destinationBackupFileName = $"{errorFile}.{orgErrorFileDateTime}";
                    File.Replace(filePath, errorFile, destinationBackupFileName, true);
                }
                else
                {
                    File.Move(filePath, errorFile);
                }
                _logger.Log(LogLevel.Error, $"{filePath} moved to {errorFile}.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }

        public void DeleteAndForget(string file, string taskInfo = null)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }
    }
}
