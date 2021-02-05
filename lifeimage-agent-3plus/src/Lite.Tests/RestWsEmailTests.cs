/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Unit Test of Rest WS Send Email Process
*/

namespace Lite.Tests
{
    using System;
    using Lite.Core.Models.Entities;
    using Lite.Services.RestWS;
    using NUnit.Framework;
    using Newtonsoft.Json;
    
    public class RestWsEmailTests
    {
        private static string bearerAccessToken;
        private static long emailId;
       
        [SetUp]
        public void RestWsGetAuthToken()
        {
            LiteRestWsToken.Instance.user = "donotreply+alexdev1agent20200612085329@lifeimage.com";
            LiteRestWsToken.Instance.password = "LITE-Y3Ca694FrOvYGO1WqPjI2A==";
            LiteRestWsToken.Instance.url = "http://localhost:8080"; // Environment.GetEnvironmentVariable("BaseUrl");
            LiteRestWsToken.Instance.resource = "user";
            bearerAccessToken = LiteRestWsToken.Instance.GetToken();
            Console.Out.WriteLine(bearerAccessToken);
            Assert.IsNotNull(bearerAccessToken);
        }
        [Test]
        public void RestWsSendEmailTest()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            EmailEntity email = new EmailEntity();
            email.instanceUid = "donotreply+alexdev1agent20200612085329@lifeimage.com";
            email.organizationCode = "organizationCode";
            email.serviceName = "serviceName";
            email.connectionName = "bourne1";
            email.emailTo = "agerner@lifeimage.com";
            email.emailSubject = "notification";
            email.emailText =
                "The golden sea its mirror spreads beneath the golden skies, and but a narrow strip between Of land and shadow lies...";
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(email, "email", baseUrl, bearerAccessToken);
            object obj = liteRestWsRequest.SendEmail(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            EmailEntity emailResponse = JsonConvert.DeserializeObject<EmailEntity>(jsonString);
            Assert.AreEqual(emailResponse.emailStatus,"success");
        }
        
    }
}