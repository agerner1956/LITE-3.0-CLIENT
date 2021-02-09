namespace Lite.Core
{
    /// <summary>
    /// Contains global constants.
    /// </summary>
    public static class Constants
    {
        public const string ProgramDataDir = "C:\\ProgramData\\LifeIMAGE LITE";
        
        /// <summary>
        /// Stored in Windows System Registry to use EventViewer.
        /// </summary>
        public const string ProductKey = "{8FF5582B-FB88-4709-A87A-1F4953B8A0D7}";

        public const string StartupFileName = "startup.config.json";

        public const string ProgramDataFolderName = "Life Image Transfer Exchange";

        public static class Dirs
        {
            public const string Meta = "meta";
            public const string Cache = "cache";
            public const string ResponseCache = "ResponseCache";
            public const string ToCloud = "toCloud";
            public const string ToRules = "toRules";
            public const string Profiles = "Profiles";
            public const string Errors = "errors";
            public const string dcmtk = "dcmtk";
            public const string share = "share";
        }

        public sealed class Extensions
        {
            /// <summary>
            /// .meta extension.
            /// </summary>
            public static Extensions MetaExt = new Extensions(".meta");

            private Extensions(string ext)
            {
                ExtValue = ext;
            }

            public string ExtValue { get; private set; }

            public override string ToString()
            {
                return ExtValue;
            }
        }

        public static class Connections
        {
            public const int maxAttempts = 10;
            public const int retryDelayMinutes = 10;
            public const int ResponseCacheExpiryMinutes = 60;
        }
    }

    public static class FileExtensions
    {
        public static string ToSearchPattern(this Lite.Core.Constants.Extensions ext)
        {
            return "*" + ext.ExtValue;
        }
    }
}
