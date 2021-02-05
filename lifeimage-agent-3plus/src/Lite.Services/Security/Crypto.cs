using Lite.Core.IoC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Lite.Core.Security
{
    /// <summary>
    ///  Crypto is the class used to secure sensitive data, whether it is at rest or in-transit.
    /// </summary>
    public class Crypto : ICrypto
    {
        private readonly SymmetricAlgorithm myAes;
        public readonly ILogger _logger;

        public Crypto(ILogger<Crypto> logger)
        {
            _logger = logger;
            myAes = Aes.Create();
        }

        public byte[] Key => myAes.Key;
        public byte[] IV => myAes.IV;

        public string Protect(string data)
        {
            if (data == null)
            {
                return null;
            }

            // if (!data.EndsWith("="))        
            if (IsBase64Encoded(data) != false)
            {
                return data;
            }

            try
            {
                // Encrypt the data using AES
                return "LITE-" + Convert.ToBase64String(EncryptStringToBytes_Aes(data, myAes.Key, myAes.IV));
            }
            catch (CryptographicException e)
            {
                _logger.Log(LogLevel.Warning, $"Data was not encrypted. An error occurred. {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }
                return null;
            }
        }

        // doesn't check if it's already protected
        public string ProtectForce(string data)
        {
            if (data == null)
            {
                return null;
            }

            try
            {
                // Encrypt the data using AES
                return "LITE-" + Convert.ToBase64String(EncryptStringToBytes_Aes(data, myAes.Key, myAes.IV));
            }
            catch (CryptographicException e)
            {
                _logger.Log(LogLevel.Warning, $"Data was not encrypted. An error occurred. {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }
                return null;
            }
        }

        public string Unprotect(string data, byte[] key, byte[] IV)
        {
            if (data != null && IsBase64Encoded(data))
            {
                try
                {
                    // Decrypt the bytes to a string.
                    return DecryptStringFromBytes_Aes(Convert.FromBase64String(data.Substring(5)), key, IV); //.Substring(5) is for "LITE-"
                }
                catch (CryptographicException e)
                {
                    _logger.Log(LogLevel.Warning, $"Data was not decrypted. An error occurred. {e.Message} {e.StackTrace}");
                    if (e.InnerException != null)
                    {
                        _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                    }
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public bool IsBase64Encoded(string data)
        {
            if (data.StartsWith("LITE-"))
            {
                return true;
            }

            if (Regex.Matches(data, "D+").Count == 0)
            {
                //all numeric password coming from cloud that looks base64 decodable. 
                return false;
            }

            try
            {
                Convert.FromBase64String(data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            _logger.Log(LogLevel.Debug, $"plainText {plainText}");
            _logger.Log(LogLevel.Debug, $"Key {BitConverter.ToString(Key)}");
            _logger.Log(LogLevel.Debug, $"IV {BitConverter.ToString(IV)}");

            // Check arguments.
            if (plainText == null)
            {
                // --> Why not? || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            }

            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(Key));
            }

            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException(nameof(IV));
            }

            byte[] encrypted;
            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using MemoryStream msEncrypt = new MemoryStream();
                using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    //Write all data to the stream.
                    swEncrypt.Write(plainText);
                }
                encrypted = msEncrypt.ToArray();
            }

            // Return the encrypted bytes from the memory stream.
            _logger.Log(LogLevel.Debug, $"returning encrypted bytes: {BitConverter.ToString(encrypted)}");

            return encrypted;
        }

        private string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            _logger.Log(LogLevel.Debug, $"cipherText {BitConverter.ToString(cipherText)}");
            _logger.Log(LogLevel.Debug, $"Key {BitConverter.ToString(Key)}");
            _logger.Log(LogLevel.Debug, $"IV {BitConverter.ToString(IV)}");

            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
            {
                throw new ArgumentNullException(nameof(cipherText));
            }

            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(Key));
            }

            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException(nameof(IV));
            }

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using MemoryStream msDecrypt = new MemoryStream(cipherText);
                using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using StreamReader srDecrypt = new StreamReader(csDecrypt);
                // Read the decrypted bytes from the decrypting stream
                // and place them in a string.
                plaintext = srDecrypt.ReadToEnd();
            }

            _logger.Log(LogLevel.Debug, $"returning plaintext {plaintext}");
            return plaintext;
        }
    }
}
