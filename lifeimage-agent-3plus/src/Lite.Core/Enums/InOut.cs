using System.Text.Json.Serialization;

namespace Lite.Core.Enums
{
    /// <summary>
    /// inOut gives a clue as to the Connection capability, whether it supports inbound or outbound communications, or both. 
    /// inbound types require starting the listening port(s).  
    /// This is the enum that defines the possible values.
    /// </summary>
    //to get string values in file instead of numbers
    [JsonConverter(typeof(JsonStringEnumConverter))] 
    public enum InOut
    {
        inbound = 0,
        outbound = 1,
        both = 2
    };
}
