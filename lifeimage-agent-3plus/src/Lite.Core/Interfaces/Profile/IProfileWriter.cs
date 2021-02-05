using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface IProfileWriter
    {
        Task SaveProfile(Profile profile);
    }

    public interface IFileProfileWriter
    {
        void Save(Profile profile, string fileName);
    }
}
