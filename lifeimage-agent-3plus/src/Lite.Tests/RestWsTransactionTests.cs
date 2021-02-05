/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Unit Tests of Rest WS Transactions methods: InitTransaction, UpdateTransaction, GetTransaction, GetTransactions (multiple records) 
*/

using System;
using System.Collections.Generic;
using Lite.Core.Models.Entities;
using Lite.Services.RestWS;
using NUnit.Framework;
using Newtonsoft.Json;


namespace Lite.Tests
{
    public class RestWsTransactionTests
    {

        private static string bearerAccessToken;
        private static long transactionId;
        
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
        public void RestWsInitTransactionTest()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            TransactionEntity transaction = new TransactionEntity();
            transaction.instanceUid = "donotreply+alexdev1agent20200612085329@lifeiminstanceUid";
            transaction.organizationCode = "organizationCode";
            transaction.serviceName = "serviceName";
            transaction.connectionName = "bourne1";
            transaction.transDirection = "inbound";
            transaction.transSize = 50;
            transaction.patientMrn = "6125268959";
            transaction.accessionNumber = "606558125410";
            transaction.studyUid = "1.2.840.10008";
            transaction.seriesUid = "1.1.1.1.1";
            transaction.sopUid = "1.1.1.1.1.1";
            transaction.transStatus = "success";
            transaction.errorCode = null;
            transaction.errorMessage = null;
            transaction.transStarted = "2020-11-26 10:32:00";
            transaction.transFinished = null;
            transaction.retryAttempt = 0;
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(transaction, "transaction", baseUrl, bearerAccessToken);
            object obj =  liteRestWsRequest.InitRecord(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            TransactionEntity transactionResponse = JsonConvert.DeserializeObject<TransactionEntity>(jsonString);
            transactionId = transactionResponse.id;
            Assert.IsNotNull(transactionResponse.id);
        }

       
       [Test]
        public void RestWsUpdateTransactionTest()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            TransactionEntity transaction = new TransactionEntity();
            transaction.instanceUid = "donotreply+alexdev1agent20200612085329@lifeiminstanceUid";
            transaction.organizationCode = "organizationCode";
            transaction.serviceName = "serviceName";
            transaction.connectionName = "bourne1";
            transaction.transDirection = "inbound";
            transaction.transSize = 50;
            transaction.patientMrn = "6125268959";
            transaction.accessionNumber = "606558125410";
            transaction.studyUid = "1.2.840.10008";
            transaction.seriesUid = "1.1.1.1.1";
            transaction.sopUid = "1.1.1.1.1.1";
            transaction.transStatus = "Failure";
            transaction.errorCode = null;
            transaction.errorMessage = null;
            transaction.transStarted = "2020-11-26 10:32:00";
            transaction.transFinished = null;
            transaction.retryAttempt = 0;
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(transaction, "transaction/"+transactionId, baseUrl, bearerAccessToken);
            object obj =  liteRestWsRequest.UpdateRecord(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            TransactionEntity transactionResponse = JsonConvert.DeserializeObject<TransactionEntity>(jsonString);
            Assert.IsNotNull(transactionResponse.id);
        }
        [Test]
        public void RestWsGetTransactionTest()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            transactionId = 50;
            TransactionEntity transaction = new TransactionEntity();
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(transaction, "transaction/"+transactionId, baseUrl, bearerAccessToken);
            object obj =  liteRestWsRequest.GetRecord(transactionId);
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            TransactionEntity transactionResponse = JsonConvert.DeserializeObject<TransactionEntity>(jsonString);
            Assert.IsNotNull(transactionResponse.id);
        }
        
        [Test]
        public void RestWsGetTransactionsTest()
        {
            string baseUrl = "http://localhost:8080/li/lite/ws";
            TransactionEntity transaction = new TransactionEntity();
            transaction.instanceUid = "donotreply+alexdev1agent20200612085329@lifeiminstanceUid";
            transaction.organizationCode = "organizationCode";
            transaction.serviceName = "serviceName";
            transaction.connectionName = "bourne1";
            transaction.transDirection = "inbound";
            transaction.transSize = 50;
            transaction.patientMrn = "6125268959";
            transaction.accessionNumber = "606558125410";
            transaction.studyUid = "1.2.840.10008";
            transaction.seriesUid = "1.1.1.1.1";
            transaction.sopUid = "1.1.1.1.1.1";
            transaction.transStatus = "Failure";
            transaction.errorCode = null;
            transaction.errorMessage = null;
            transaction.transStarted = "2020-11-26 10:32:00";
            transaction.transFinished = null;
            transaction.retryAttempt = 0;
            LiteRestWsRequest liteRestWsRequest =
                new LiteRestWsRequest(transaction, "transactions", baseUrl, bearerAccessToken);
            object obj =  liteRestWsRequest.GetRecords(liteRestWsRequest.GetWsRequest());
            var jsonString = JsonConvert.SerializeObject(obj);
            Console.Out.WriteLine(jsonString);
            List<TransactionEntity> transactions = JsonConvert.DeserializeObject<List<TransactionEntity>>(jsonString);
            Assert.IsNotNull(transactions);
        }
    }
}