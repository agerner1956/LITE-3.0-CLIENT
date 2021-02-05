using Lite.Core.Enums;
using System.Collections.Generic;
using System.Text.Json;

namespace Lite.Core.Json
{
    public class JsonHelper
    {
        public static Dictionary<string, string> DeserializeFromMap(string json)
        {
            JsonSerializerOptions settings = new JsonSerializerOptions
            {
                //TypeNameHandling = TypeNameHandling.Objects
            };

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, settings);
        }
    }
}
