using System.Text.Json.Serialization;

namespace Lite.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AckMode
    {
        Original = 0,
        Enhanced = 1,
        Custom = 2
    };
}
