/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: HL7 Entity Supporting Rest WS Client 
*/


namespace Lite.Core.Models.Entities
{
   public class HL7Entity
    {
      public long id { get; set; }
       public string instanceUid { get; set; } // property in default.json "username": 
       public string organizationCode { get; set; } // property in default.js
       public string serviceName { get; set; } // property in default.json
       public string connectionName  { get; set; } // property in default.json
       public string patientMrn { get; set; } // Patient medical record number
       public string accessionNumber { get; set; }
       public string hl7Status { get; set; } // success, failed, fatal
       public string errorCode { get; set; } // errors classifier key
       public string errorMessage { get; set; } // errors classifier value
       public string hl7Timestamp { get; set; } // YYYY-MM-DD HH:mm:SS
       public int retryAttempt { get; set; } // (0-max property in default.json) 
       
    }
}