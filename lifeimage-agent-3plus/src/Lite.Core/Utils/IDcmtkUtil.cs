namespace Lite.Core.Utils
{
    public interface IDcmtkUtil
    {
        void ExtractWindowsDCMTK();
        bool ConfigureDcmtk(bool install, Profile profile, string currentProfile);
    }
}
