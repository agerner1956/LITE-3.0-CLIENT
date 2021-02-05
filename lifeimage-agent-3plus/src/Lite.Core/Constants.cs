namespace Lite.Core
{
    /// <summary>
    /// Contains global constants.
    /// </summary>
    public static class Constants
    {
        public const string ProgramDataDir = "C:\\ProgramData\\LifeIMAGE LITE";
        public const string ProfilesDir = "Profiles";

        public static class Profiles
        {

        }     

        public static class Dirs
        {
            public const string Meta = "meta";
            public const string Cache = "cache";
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
