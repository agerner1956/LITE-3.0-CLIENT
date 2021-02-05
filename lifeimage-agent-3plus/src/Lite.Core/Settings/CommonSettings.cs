namespace Lite.Core.Common
{
    public class CommonSettings : ISettings
    {
        public SplunkConfig SplunkConfig { get; set; }
    }

    public class SplunkConfig
    {
        public string Uri { get; set; }
        public string Token { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Uri) && !string.IsNullOrEmpty(Token);
        }
    }
}
