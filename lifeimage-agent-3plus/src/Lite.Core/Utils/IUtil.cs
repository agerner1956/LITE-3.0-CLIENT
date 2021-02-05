using Lite.Core.Enums;
using System.Diagnostics;

namespace Lite.Core.Utils
{
    public interface IUtil
    {
        IDiskUtils DiskUtils { get; }

        bool ConfigureService(bool install);
        bool CreateService(ProcessStartInfo Info);
        //bool StartStopService(bool start);
        string GetTempFolder(string path = "");
        string EnvSeparatorChar();
        Priority GetPriority(ushort priority);
        ushort GetPriority(Priority priority);
        void WriteAllTextWithBackup(string path, string contents);
        string RemoveInvalidPathAndFileCharacters(string inputString);
    }
}
