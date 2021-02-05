using System.Threading.Tasks;

namespace Lite.Core.Interfaces
{
    public interface ILoggerManager
    {
        Logger Init(Logger logger, string account=null);        
        Task RotateLogs();
    }
}
