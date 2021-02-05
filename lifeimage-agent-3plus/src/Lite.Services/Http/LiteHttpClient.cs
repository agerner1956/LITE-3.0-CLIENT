using Lite.Core.Connections;
using Lite.Core.Guard;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Lite.Services.Http
{
    public interface ILiteHttpClient : IDisposable
    {
        HttpConnection Connection { get; }
        HttpClient GetClient(HttpConnection connection);
        CookieCollection GetCookies(string resourceURL);
        void DumpHttpClientDetails();        
    }

    public sealed class LiteHttpClient : ILiteHttpClient
    {
        public TimeSpan httpRequestTimeout = new TimeSpan(days: 0, hours: 0, minutes: 30, seconds: 0, milliseconds: 0);

        private HttpClientHandler httpClientHandler;
        private HttpClient httpClient;

        private readonly ILogger _logger;
        private readonly IProfileStorage _profileStorage;

        public LiteHttpClient(
            IProfileStorage profileStorage,
            ILogger<LiteHttpClient> logger)
        {
            _profileStorage = profileStorage;
            _logger = logger;
        }

        public HttpConnection Connection { get; private set; }

        public HttpClient GetClient(HttpConnection connection)
        {
            // TODO: load existing http client from HttpConnectionManagerBase<T>
            return InitHttpClientBasedOnConnection(connection);
        }

        public CookieCollection GetCookies(string resourceURL)
        {
            if (httpClient == null)
            {
                throw new InvalidOperationException("HttpClient is not initialized!");
            }
            var cookies = httpClientHandler.CookieContainer.GetCookies(new Uri(resourceURL));
            return cookies;
        }

        public bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void DumpHttpClientDetails()
        {
            if (httpClient == null)
            {
                throw new InvalidOperationException("HttpClient is not initialized!");
            }

            Throw.IfNull(Connection);

            try
            {
                var cookies = httpClientHandler.CookieContainer.GetCookies(new Uri(Connection.URL));
                _logger.LogCookies(cookies);
            }
            catch
            {
                // eat it
            }
        }

        public void Dispose()
        {
            if (httpClient == null)
            {
                return;
            }

            try
            {
                httpClient.CancelPendingRequests();

                // todo: probaly this operation is not needed
                httpClient.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private HttpClient InitHttpClientBasedOnConnection(HttpConnection connection)
        {
            Connection = connection;

            Throw.IfNull(Connection);

            httpClientHandler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            // set the redirect behavior
            httpClientHandler.AllowAutoRedirect = false;
            httpClientHandler.MaxConnectionsPerServer = Connection.maxConnectionsPerServer;


            //#if DEBUG
            httpClientHandler.ServerCertificateCustomValidationCallback += RemoteCertificateValidationCallback;
            //#endif

            // allow cookies
            httpClientHandler.UseCookies = true;

            //httpClientHandler.MaxRequestContentBufferSize = 2147483647; //default is 2GB so just stating the obvious here in case there is trouble.

            // create the client
            httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = httpRequestTimeout  //default is 100 seconds and will cause task cancellations that are hard to locate root cause.
            };

            //httpClient.MaxResponseContentBufferSize = 2048000000; //default is 2GB so just stating the obvious here in case there is trouble


            // set the headers
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"LITE {_profileStorage.Current.runningCodeVersion}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/javascript, application/json, application/dicom+xml");
            httpClient.DefaultRequestHeaders.Add("Accept", "multipart/related; type=\"application/dicom\"");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
            httpClient.DefaultRequestHeaders.ConnectionClose = true;

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

            return httpClient;
        }
    }
}
