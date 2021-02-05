using Lite.Core.Interfaces;
using System.Threading.Tasks;

namespace Lite.Services
{
    public static class ILiteEngineExtensions
    {
        public static Task Restart(this ILiteEngine liteEngine)
        {
            liteEngine.Stop();
            liteEngine.Start();

            return Task.CompletedTask;
        }
    }
}
