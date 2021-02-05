using Lite.Core;

namespace Lite.Services
{
    public interface IProfileStorage
    {
        Profile Current { get; }
        void Set(Profile profile);
    }
}
