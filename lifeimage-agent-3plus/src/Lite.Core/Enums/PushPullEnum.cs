using System.Text.Json.Serialization;

namespace Lite.Core.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))] //to get string values in file instead of numbers
    public enum PushPullEnum
    {
        push = 0,
        pull = 1,
        both = 2
    };
}
