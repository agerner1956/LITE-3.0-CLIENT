using System.Text.Json.Serialization;

namespace Lite.Core.Enums
{
    /// <summary>        
    /// ConnectionType indicates the type of Connection and is used in processing to activate special handling
    /// unique to each type, if any.  For example, http connections may need to know when network connectivity is
    /// disrupted. This is the enum that defines the known ConnectionTypes.
    /// </summary>    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionType
    {
        http = 0,
        dicom = 1,
        cloud = 2,
        file = 3,
        hl7 = 4,
        dcmtk = 5,
        lite = 6,
        other = 7
    }
}
