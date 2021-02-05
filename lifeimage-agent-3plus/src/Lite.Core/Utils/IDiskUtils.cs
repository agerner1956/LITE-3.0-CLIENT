using System.Collections.Generic;

namespace Lite.Core.Utils
{
    public interface IDiskUtils
    {
        List<string> DirSearch(string sDir, string pattern);
        void CleanUpDirectory(string startLocation, int retentionMinutes = 5);
        byte[] ReadBytesFromFile(string fileName);
        bool IsDiskAvailable(string path, Profile profile, long length = 0);
        void MoveFileToErrorFolder(string tempPath, string filePath, string name = "");
    }
}
