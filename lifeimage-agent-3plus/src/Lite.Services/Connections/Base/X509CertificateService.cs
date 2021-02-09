using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Lite.Services.Connections
{
    public interface IX509CertificateService
    {
        string ServicePoint { get; }

        X509Certificate GetServerCertificate(string connectionName);
    }

    public sealed class X509CertificateService : IX509CertificateService
    {
        public const string ServicePointName = "localhost";

        public string ServicePoint { get; } = ServicePointName;

        public X509Certificate GetServerCertificate(string connectionName)
        {
            X509Store store = null;
            X509Certificate serverCertificate = null;

            try
            {
                store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

                //                            X509Store store = new X509Store(X509StoreName, X509StoreLocation);
                store.Open(OpenFlags.OpenExistingOnly);
                X509Certificate2Collection certs = null;
                try
                {
                    certs = store.Certificates.Find(X509FindType.FindByIssuerName, connectionName, false);
                }
                catch (ArgumentException)
                {
                    //create the self-signed cert
                    var cert = buildSelfSignedServerCertificate(ServicePoint);
                    store.Certificates.Add(cert);
                    certs.Add(cert);
                }

                if (certs.Count > 0)
                {
                    serverCertificate = certs[0];
                }
                else
                {
                    //create the self-signed cert
                    var cert = buildSelfSignedServerCertificate(ServicePoint);
                    store.Certificates.Add(cert);

                    certs.Add(cert);
                    serverCertificate = cert;
                    //                                serverCertificate = X509Certificate.CreateFromCertFile("ca.cer");
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                store.Dispose();
            }

            return serverCertificate;
        }

        private X509Certificate2 buildSelfSignedServerCertificate(string name)
        {
            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(Environment.MachineName);

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={name}");

            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DigitalSignature, false));


            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            request.CertificateExtensions.Add(sanBuilder.Build());

            var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
            // try
            // {
            //     certificate.FriendlyName = name;
            // }
            // catch (Exception) { }

            return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "WeNeedASaf3rPassword"),
                "WeNeedASaf3rPassword", X509KeyStorageFlags.UserKeySet);
        }
    }
}
