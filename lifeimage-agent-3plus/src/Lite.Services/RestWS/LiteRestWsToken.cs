/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Get Bearer Authentication Token from LifeImage REST WS Server.
   ###          Token Expiration Interval - configurable. Specified in Application properties.
 */

using System;
using RestSharp;

namespace Lite.Services.RestWS
{
    public sealed class LiteRestWsToken
    {
        private LiteRestWsToken()
        {
        }
        private static LiteRestWsToken instance = null;
      

        public static LiteRestWsToken Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LiteRestWsToken();
                }

                return instance;
            }
        }

        public static string token { get; set; }
        public string url { get; set; }
        public string user { get; set; }
        public string password { get; set; }
        public string resource { get; set; }
        
        private static int currentTokenTimestamp;
      
        private static int currentTokenExpirationInterval = 24*60*60; 

        private static string currentToken { get; set; }
        
        public string GetToken()
        {
           
            int newTokenTimestamp = GetTokenTimestamp();
            int delta = newTokenTimestamp - currentTokenTimestamp;
            if (token==null || delta > currentTokenExpirationInterval)
            {
              
                RestClient restClient = new RestClient();
                RestRequest restRequest = new RestRequest(resource, Method.POST);
                var client = new RestClient(url);
                restRequest.AddParameter("user", user);
                restRequest.AddParameter("password", password);
                IRestResponse response = client.Execute(restRequest);
                token = response.Content;
                currentToken = token;
                currentTokenTimestamp = newTokenTimestamp;
            }
            else
            {
                token = currentToken;
            } 
            return token;
        }

        private int GetTokenTimestamp()
        {
            var timestamp = (int) (DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
            return timestamp;
        }
   }
}