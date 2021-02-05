namespace Lite.Core.Security
{
    public interface ICrypto
    {
        string Protect(string data);
        string ProtectForce(string data);
        string Unprotect(string data, byte[] key, byte[] IV);
        bool IsBase64Encoded(string data);

        public byte[] Key { get; }
        public byte[] IV { get; }
    }
}
