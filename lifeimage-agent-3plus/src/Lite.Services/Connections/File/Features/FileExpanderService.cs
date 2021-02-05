using System;

namespace Lite.Services.Connections.Files.Features
{
    public interface IFileExpanderService
    {
        /// <summary>
        /// Expands the filepath for ~ home dir syntax
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public string Expand(string filepath);
    }

    public sealed class FileExpanderService : IFileExpanderService
    {
        public string Expand(string filepath)
        {
            string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
                               Environment.OSVersion.Platform == PlatformID.MacOSX)
                ? Environment.GetEnvironmentVariable("HOME")
                : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

            return filepath.Replace("~", homePath);
        }
    }
}
