using Lite.Core.Connections;
using Lite.Core.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lite.Core.Json
{
    public sealed class ConnectionJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return (typeToConvert == typeof(Connection));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return new ConnectionJsonConverter();
        }
    }

    public sealed class ConnectionJsonConverter : JsonConverter<Connection>
    {
        private readonly System.Text.Encoding _encoding;

        public ConnectionJsonConverter()
        {
            _encoding = System.Text.Encoding.UTF8;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            bool result = typeof(Connection).IsAssignableFrom(typeToConvert);
            return result;
        }

        public override Connection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {            
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var jsonDocument = JsonDocument.ParseValue(ref reader);
            var json = jsonDocument.RootElement.GetRawText();

            var connection = JsonSerializer.Deserialize<Connection>(json);
            var connType = connection.connType;
            switch (connType)
            {
                case ConnectionType.dicom:
                    {
                        var dicom = JsonSerializer.Deserialize<DICOMConnection>(json);
                        return dicom;
                    }                    
                case ConnectionType.cloud:
                    {
                        var cloud = JsonSerializer.Deserialize<LifeImageCloudConnection>(json);
                        return cloud;                                                
                    }                    
                case ConnectionType.file:
                    {
                        var file = JsonSerializer.Deserialize<FileConnection>(json);
                        return file;
                    }                    
                case ConnectionType.hl7:
                    {
                        var hl7 = JsonSerializer.Deserialize<HL7Connection>(json);
                        return hl7;                       
                    }
                case ConnectionType.dcmtk:
                    {
                        var dcmtk = JsonSerializer.Deserialize<DcmtkConnection>(json);
                        return dcmtk;
                    }
                    
                case ConnectionType.lite:
                    {
                        var lite = JsonSerializer.Deserialize<LITEConnection>(json);
                        return lite;
                    }
                    
                case ConnectionType.other:
                default:
                    return connection;                    
            }
        }

        public override void Write(Utf8JsonWriter writer, Connection value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }

    [Obsolete]
    public class ProfileConverter : JsonConverter<Connection>
    {
        public override Connection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var connection = JsonSerializer.Deserialize<Connection>(ref reader);
            return connection;
        }

        public override void Write(Utf8JsonWriter writer, Connection value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        //public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        //{

        //    JObject jo = JObject.Load(reader);
        //    var connType = (ConnectionType)Enum.Parse(typeof(ConnectionType), (jo["connType"].Value<string>()));


        //    switch (connType)
        //    {
        //        case ConnectionType.dicom:
        //            return jo.ToObject<DICOMConnection>(serializer);
        //        case ConnectionType.cloud:
        //            return jo.ToObject<LifeImageCloudConnection>(serializer);
        //        case ConnectionType.file:
        //            return jo.ToObject<FileConnection>(serializer);
        //        case ConnectionType.hl7:
        //            return jo.ToObject<HL7Connection>(serializer);
        //        case ConnectionType.dcmtk:
        //            return jo.ToObject<DcmtkConnection>(serializer);
        //        case ConnectionType.lite:
        //            return jo.ToObject<LITEConnection>(serializer);
        //        case ConnectionType.other:
        //        default:
        //            return jo.ToObject<Profile>(serializer);
        //    }

        //}
    }
}
