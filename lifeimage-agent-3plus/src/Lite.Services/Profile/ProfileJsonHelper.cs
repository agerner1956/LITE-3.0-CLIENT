using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Core.Json;
using System.IO;
using System.Text.Json;

namespace Lite.Services
{
    public sealed class ProfileJsonHelper : IProfileJsonHelper
    {
        public Profile DeserializeObject(string json)
        {
            try
            {
                //todo: use converter
                //JsonSerializerSettings settings = new JsonSerializerSettings
                //{
                //    Converters = { Profile.profileConverter }
                //};

                JsonSerializerOptions settings = new JsonSerializerOptions();                
                settings.Converters.Add(new ConnectionJsonConverterFactory());

                // If this fails exception handler creates a new Profile and puts bad json into jsonInError
                Profile profile = JsonSerializer.Deserialize<Profile>(json, settings);
                return profile;
            }
            catch (System.Text.Json.JsonException e)
            {
                Profile profileInError = new Profile();

                profileInError.errors.Add(e.Message);
                profileInError.jsonInError = json;

                return profileInError;
            }
        }

        public Profile DeserializeFromStream(Stream stream)
        {
            byte[] buffer = new byte[stream.Length];

            stream.Read(buffer, 0, (int)stream.Length);

            string json = System.Text.Encoding.UTF8.GetString(buffer);

            JsonSerializerOptions settings = new JsonSerializerOptions
            {
                //TypeNameHandling = TypeNameHandling.Objects               
            };

            return DeserializeObject(json);
        }
    }
}
