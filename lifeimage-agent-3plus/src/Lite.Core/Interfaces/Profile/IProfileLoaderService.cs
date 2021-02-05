namespace Lite.Core.Interfaces
{
    public interface IProfileLoaderService
    {
        Profile Load(string fileName);
        Profile LoadStartupConfiguration(string startupConfigFilePath);
    }
}
