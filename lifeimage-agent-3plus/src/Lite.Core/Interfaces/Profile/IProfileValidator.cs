using System.Collections.Generic;

namespace Lite.Core.Interfaces
{
    public interface IProfileValidator
    {
        List<string> FullValidate(Profile profile, string profileJSON);
    }
}
