using Lite.Core.Models;
using Microsoft.CodeAnalysis.Scripting;
using System.Threading.Tasks;

namespace Lite.Core.Interfaces.Scripting
{
    public interface IScriptService
    {
        void Compile(Core.Models.Script item);
        Task<ScriptState> RunAsync(Core.Models.Script item, RoutedItem routedItem);
    }
}
