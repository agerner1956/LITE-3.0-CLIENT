/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Email Entity Supporting Rest WS Client 
*/

namespace Lite.Core.Models.Entities
{
    public class EmailEntity
    {
      public long id { get; set; }
      public string instanceUid { get; set; } // property in default.json "username": 
      public string organizationCode { get; set; } // property in default.js
      public string serviceName { get; set; } // property in default.json
      public string connectionName{ get; set; } // property in default.json
      public string emailFrom{ get; set; }
      public string emailTo { get; set; }
      public string emailSubject { get; set; }
      public string emailText { get; set; }
      public string emailTimestamp { get; set; } // YYYY-MM-DD HH:mm:ss
      public string emailStatus { get; set; } // success/failure
      
    }
}