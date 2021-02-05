/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Transaction Entity Supporting Rest WS Client 
*/

namespace Lite.Core.Models.Entities
{
    public class TransactionEntity
    {
     /*   public long TransUid
        {
            get => transUid;
            set => transUid = value;
        }

        public string InstanceUid
        {
            get => instanceUid;
            set => instanceUid = value;
        }

        public string OrganizationCode
        {
            get => organizationCode;
            set => organizationCode = value;
        }

        public string ServiceName
        {
            get => serviceName;
            set => serviceName = value;
        }

        public string ConnectionName
        {
            get => connectionName;
            set => connectionName = value;
        }

        public string TransDirection
        {
            get => transDirection;
            set => transDirection = value;
        }

        public int TransSize
        {
            get => transSize;
            set => transSize = value;
        }

        public string PatientMrn
        {
            get => patientMrn;
            set => patientMrn = value;
        }

        public string AccessionNumber
        {
            get => accessionNumber;
            set => accessionNumber = value;
        }

        public string StudyUid
        {
            get => studyUid;
            set => studyUid = value;
        }

        public string SeriesUid
        {
            get => seriesUid;
            set => seriesUid = value;
        }

        public string SopUid
        {
            get => sopUid;
            set => sopUid = value;
        }

        public string TransStatus
        {
            get => transStatus;
            set => transStatus = value;
        }

        public string ErrorCode
        {
            get => errorCode;
            set => errorCode = value;
        }

        public string ErrorMessage
        {
            get => errorMessage;
            set => errorMessage = value;
        }

        public string TransStarted
        {
            get => transStarted;
            set => transStarted = value;
        }

        public string TransFinished
        {
            get => transFinished;
            set => transFinished = value;
        }

        public int RetryAttempt
        {
            get => retryAttempt;
            set => retryAttempt = value;
        }

       [JsonPropertyName("transId")]
       private long transUid;
       [JsonPropertyName("instanceUid")]
       private string instanceUid; // property in default.json "username": 
       [JsonPropertyName("organizationCode")]
       private string organizationCode; // property in default.js
       [JsonPropertyName("serviceName")]
       private string serviceName; // property in default.json
       [JsonPropertyName("connectionName")]
       private string connectionName; // property in default.json
       [JsonPropertyName("transDirection")]
       private string transDirection; // inbound/outbound
       [JsonPropertyName("transSize")]
       private int transSize; // MB
       [JsonPropertyName("patientMrn")]
       private string patientMrn; // Patient medical record number
       [JsonPropertyName("accessionNumber")]
       private string accessionNumber;
       [JsonPropertyName("studyUid")]
       private string studyUid;
       [JsonPropertyName("seriesUid")]
       private string seriesUid;
       [JsonPropertyName("sopUid")]
       private string sopUid;
       [JsonPropertyName("transStatus")]
       private string transStatus; // success, failed, fatal
       [JsonPropertyName("errorCode")]
       private string errorCode; // errors classifier key
       [JsonPropertyName("errorMessage")]
       private string errorMessage;// errors classifier value
       [JsonPropertyName("transStarted")]
       private string transStarted;// YYYY-MM-DD HH:mm:SS
       [JsonPropertyName("transFinished")]
       private string transFinished;// YYYY-MM-DD HH:mm:SS
       [JsonPropertyName("retryAttempt")]
       private int retryAttempt;// (0-max property in default.json) */
       public long id { get; set; }
       public string instanceUid { get; set; } // property in default.json "username": 
       public string organizationCode {  get; set; } // property in default.js
       public string serviceName { get; set; }  // property in default.json
       public string connectionName { get; set; }  // property in default.json
       public string transDirection { get; set; } // inbound/outboundt
       public int transSize { get; set; } // MBpublic
       public string patientMrn { get; set; } // Patient medical record number
       public string accessionNumber { get; set; }
       public string studyUid  { get; set; }
       public string seriesUid { get; set; }
       public string sopUid { get; set; }
       public string transStatus { get; set; } // success, failed, fatal
       public string errorCode { get; set; } // errors classifier key\
       public string errorMessage { get; set; }// errors classifier value
       public string transStarted { get; set; }// YYYY-MM-DD HH:mm:SS
       public string transFinished{ get; set; } // YYYY-MM-DD HH:mm:SS
       public int retryAttempt { get; set; }// (0-max property in default.json) */
    }
}