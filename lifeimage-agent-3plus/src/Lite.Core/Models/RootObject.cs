using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    public class RootObject
    {
        [JsonPropertyName("ImagingStudy")]
        public List<ImagingStudy> ImagingStudy { get; set; } = new List<ImagingStudy>();
    }
}
