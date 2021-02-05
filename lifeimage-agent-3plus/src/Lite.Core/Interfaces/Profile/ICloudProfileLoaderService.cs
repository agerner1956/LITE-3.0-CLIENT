using Lite.Core.Connections;
using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface ICloudProfileLoaderService
    {
        Task<Profile> GetAgentConfigurationFromCloud(LifeImageCloudConnection conn, string rowVersion, bool _overrideVersionAndModifiedDate);
        Task<Profile> LoadProfile(Profile source, LifeImageCloudConnection conn);
    }
}
