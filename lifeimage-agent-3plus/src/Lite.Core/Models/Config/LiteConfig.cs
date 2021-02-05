namespace Lite.Core
{
    public class LiteConfig
    {
        public LiteConfig(string startConfigFilePath, string tempPath)
        {
            StartupConfigFilePath = startConfigFilePath;
            TempPath = tempPath;
        }
        public string StartupConfigFilePath { get; private set; }
        public string TempPath { get; private set; }
    }
}
