using System.IO;

namespace Lite.Core.Interfaces
{
    public interface IProfileJsonHelper
    {
        Profile DeserializeObject(string json);
        Profile DeserializeFromStream(Stream stream);
    }
}
