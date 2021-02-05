using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using System.IO;

namespace Lite.Services.Config
{
    public sealed class LiteConfigService : ILiteConfigService
    {
        public const string StartupFileName = "startup.config.json";

        private readonly IUtil _util;

        public LiteConfigService(IUtil util)
        {
            _util = util;
        }

        public LiteConfig GetDefaults()
        {
            string startupConfigFilePath = _util.GetTempFolder() + Path.DirectorySeparatorChar + Constants.ProfilesDir + Path.DirectorySeparatorChar + StartupFileName;
            string tempPath = _util.GetTempFolder() + Path.DirectorySeparatorChar + "tmp";

            return new LiteConfig(startupConfigFilePath, tempPath);
        }
    }
}
