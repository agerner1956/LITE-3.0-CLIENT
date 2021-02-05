/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Unit Tests of Rest WS HL7 methods: InitHL7, UpdateHL7, GetHL7, GetHL7s (multiple records) 
*/


namespace Lite.Tests
{
    using System;
    using System.Collections.Generic;
    using Lite.Core.Models.Entities;
    using Lite.Services.RestWS;
    using NUnit.Framework;
    using Newtonsoft.Json;

    public class RestWsHl7Tests
    {
        private static string bearerAccessToken;
        private static long hl7Id;

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
        public void RestWsInitHl7Test()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            HL7Entity hl7 = new HL7Entity();
            hl7.instanceUid = "donotreply+alexdev1agent20200612085329@lifeimage.com";
            hl7.organizationCode = "organizationCode";
            hl7.serviceName = "serviceName";
            hl7.connectionName = "bourne1";
            hl7.patientMrn = "6125268959";
            hl7.accessionNumber = "606558125410";
            hl7.hl7Status = "init";
            hl7.errorCode = null;
            hl7.errorMessage = null;
            hl7.hl7Timestamp = "2020-11-26 10:32:00";
            hl7.retryAttempt = 0;
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(hl7, "hl7", baseUrl, bearerAccessToken);
            object obj = liteRestWsRequest.InitRecord(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            HL7Entity hl7Response = JsonConvert.DeserializeObject<HL7Entity>(jsonString);
            hl7Id = hl7Response.id;
            Assert.IsNotNull(hl7Response.id);
        }


        [Test]
        public void RestWsUpdateHl7Test()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            HL7Entity hl7 = new HL7Entity();
            hl7.instanceUid = "donotreply+alexdev1agent20200612085329@lifeimage.com";
            hl7.organizationCode = "organizationCode";
            hl7.serviceName = "serviceName";
            hl7.connectionName = "bourne1";
            hl7.patientMrn = "6125268959";
            hl7.accessionNumber = "606558125410";
            hl7.hl7Status = "init";
            hl7.errorCode = null;
            hl7.errorMessage = null;
            hl7.hl7Timestamp = "2020-11-26 10:32:00";
            hl7.retryAttempt = 0;
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(hl7, "hl7/" + hl7Id, baseUrl, bearerAccessToken);
            object obj = liteRestWsRequest.UpdateRecord(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            HL7Entity hl7Response = JsonConvert.DeserializeObject<HL7Entity>(jsonString);
            Assert.IsNotNull(hl7Response.id);
        }
        [Test]
        public void RestWsGetHl7Test()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            hl7Id = 1;
            HL7Entity hl7 = new HL7Entity();
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(hl7, "transaction/"+hl7Id, baseUrl, bearerAccessToken);
            object obj =  liteRestWsRequest.GetRecord(hl7Id);
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            HL7Entity hl7Response = JsonConvert.DeserializeObject<HL7Entity>(jsonString);
            Assert.IsNotNull(hl7Response.id);
        }
        [Test]
        public void RestWsGetHl7sTest()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            HL7Entity hl7 = new HL7Entity();
            hl7.instanceUid = "donotreply+alexdev1agent20200612085329@lifeimage.com";
            hl7.organizationCode = "organizationCode";
            hl7.serviceName = "serviceName";
            hl7.connectionName = "bourne1";
            hl7.patientMrn = "6125268959";
            hl7.accessionNumber = "606558125410";
            hl7.hl7Status = "init";
            hl7.errorCode = null;
            hl7.errorMessage = null;
            hl7.hl7Timestamp = "2020-11-26 10:32:00";
            hl7.retryAttempt = 0;
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(hl7, "hl7s", baseUrl, bearerAccessToken);
            object obj = liteRestWsRequest.GetRecords(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            List<HL7Entity> hl7s = JsonConvert.DeserializeObject<List<HL7Entity>>(jsonString);
            Assert.IsNotNull(hl7s);
        }
    }
}