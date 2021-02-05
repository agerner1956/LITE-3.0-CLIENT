using System;
using System.Security.Cryptography;
using System.Text;

namespace Lite.Services.Security
{
    public class HashHelper
    {
        private static readonly System.Text.Encoding encoding = Encoding.UTF8;

        private readonly HashAlgorithm _hashAlgorithm;

        public HashHelper(HashAlgorithm algorithm)
        {
            _hashAlgorithm = algorithm;
        }

        public string GetHash(string input)
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = _hashAlgorithm.ComputeHash(encoding.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        /// <summary>
        /// Verify a hash against a string.
        /// </summary>        
        /// <param name="input"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool VerifyHash(string input, string hash)
        {
            // Hash the input.
            var hashOfInput = GetHash(input);

            // Create a StringComparer an compare the hashes.
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            return comparer.Compare(hashOfInput, hash) == 0;
        }
    }
}
