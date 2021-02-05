/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Methods, supporting generic HTTP Client Requests: InitRecord,UpdateRecord,DeleteRecord,GetRecord,GetRecords
 */

using Lite.Core.Interfaces.RestWS;


namespace Lite.Services.RestWS
{
    public class LiteRestWsRequest : ILiteRestWSRequest
    {
        private object _request;
        private string _wsMethod;
        private string _baseUrl;
        private string _bearerAccessToken;
       
        public LiteRestWsRequest(object request, string wsMethod, string baseUrl, string bearerAccessToken)
        {
                _request = request;
                _wsMethod = wsMethod;
                _baseUrl = baseUrl;
                _bearerAccessToken = bearerAccessToken;
         }
        
        public object GetWsRequest() 
        {
            return _request;
        }
        public string GetWsMethod() 
        {
            return _wsMethod;
        }
        public object InitRecord(object request)
        {
            var obj = PopulateHTTPClientProcess
                .PopulateHttpClient(_baseUrl, _wsMethod, "POST", _bearerAccessToken, _request).Result;
            return obj;
        }

        public object UpdateRecord(object request)
        {
            var obj = PopulateHTTPClientProcess
                .PopulateHttpClient(_baseUrl, _wsMethod, "PUT", _bearerAccessToken, _request).Result;
             return obj;
        }

        public object DeleteRecord(long id)
        {
            var obj = PopulateHTTPClientProcess
                .PopulateHttpClient(_baseUrl, _wsMethod, "DELETE", _bearerAccessToken, _request).Result;
             return obj;
        }

        public object GetRecord(long id)
        {
            var obj = PopulateHTTPClientProcess
                .PopulateHttpClient(_baseUrl, _wsMethod, "GET", _bearerAccessToken, _request).Result;
            return obj;
        }

        public object GetRecords(object request)
        {
            var obj = PopulateHTTPClientProcess
                .PopulateHttpClient(_baseUrl, _wsMethod, "POST", _bearerAccessToken, _request).Result;
           return obj;
        }
        
        public object SendEmail(object request)
        {
            var obj = PopulateHTTPClientProcess
                .PopulateHttpClient(_baseUrl, _wsMethod, "POST", _bearerAccessToken, _request).Result;
            return obj;
        }
    }
}