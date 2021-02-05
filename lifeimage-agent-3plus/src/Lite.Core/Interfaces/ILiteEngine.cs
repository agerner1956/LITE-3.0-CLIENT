using System;
using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface ILiteEngine : IDisposable
    {
        void Start();
        Task Process();
        void Stop();
    }

    public interface ILitePurgeService
    {
        Task Purge(int taskID);
    }

    public interface IRegistryHelper
    {
        public void RegisterKey(string path, string value);
        public void RegisterProduct(string version);
    }
}
