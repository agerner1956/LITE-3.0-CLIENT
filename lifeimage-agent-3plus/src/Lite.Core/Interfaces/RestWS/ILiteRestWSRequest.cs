/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Rest WS Client Generic Methods Interface: InitRecord, UpdateRecord, DeleteRecord, GetRecord, GetRecords
*/


namespace Lite.Core.Interfaces.RestWS
{
    public interface ILiteRestWSRequest
    {

        object InitRecord(object request);
        object UpdateRecord(object request);
        object DeleteRecord(long id);
        object GetRecord(long id); 
        object GetRecords(object request);
    }
}