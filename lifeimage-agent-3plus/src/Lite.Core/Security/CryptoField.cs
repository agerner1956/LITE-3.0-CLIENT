using Lite.Core.IoC;
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Lite.Core.Security
{
    /// <summary>
    /// CryptoField accepts plain text and converts it for secure storage.
    /// </summary>
    public sealed class CryptoField
    {
        private readonly ICrypto _crypto;

        public CryptoField()
        {
            _crypto = CryptoStatic.Instance();
        }

        /// <summary>
        /// password is the private class storage for Password.
        /// Password has special getters and setters to handle encryption.
        /// </summary>
        private string field;

        /// <summary>
        /// Password is required to connect to a secured resource.
        /// When password is set(command line, loaded from json, read from cloud) it is checked to see whether it is secure.If it is not secure, it is secured with AES encryption. 
        /// If loadProfileFile= is the same file as saveProfileFile=, the original cleartext password will be over-written with the secured password, along with the sharedKey and IV.
        /// When password is retrieved, it will decrypt if necessesary.
        /// </summary>
        /// <returns></returns>
        [JsonPropertyName("Field")]
        public string Field
        {
            get
            {
                return field;
            }
            set
            {
                field = _crypto.Protect(value);
                sharedKey = Convert.ToBase64String(_crypto.Key);
                IV = Convert.ToBase64String(_crypto.IV);
            }
        }

        public string GetUnprotectedField()
        {
            if (sharedKey == null)
            {
                Logger.logger.Log(TraceEventType.Warning, $"Missing sharedKey.");
                return field;
            }

            if (IV == null)
            {
                Logger.logger.Log(TraceEventType.Warning, $"Missing IV.");
                return field;

            }
            var temp = _crypto.Unprotect(field, Convert.FromBase64String(sharedKey), Convert.FromBase64String(IV));
            return temp;
        }

        /// <summary>
        /// sharedKey is used to store the encription key to a secured resource.  The specifics are defined by the type of Connection.For example, an liCloud connection saves its password using AES, along with the Key and IV.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("sharedKey")]
        public string sharedKey { get; set; }

        /// <summary>
        /// IV, or initialization vector, is used in AES Encription.  For example, an liCloud connection saves its passowrd using AES along with the Key and IV.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("IV")]
        public string IV { get; set; }
    }
}
