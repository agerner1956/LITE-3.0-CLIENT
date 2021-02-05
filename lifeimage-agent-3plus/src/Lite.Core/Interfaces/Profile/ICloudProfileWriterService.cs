using Lite.Core.Connections;
using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface ICloudProfileWriterService
    {
        Task PutConfigurationToCloud(Profile profile, LifeImageCloudConnection conn);
    }
}
