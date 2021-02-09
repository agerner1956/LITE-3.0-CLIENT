using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface IProfileWriter
    {
        Task SaveProfile(Profile profile);
    }
}
