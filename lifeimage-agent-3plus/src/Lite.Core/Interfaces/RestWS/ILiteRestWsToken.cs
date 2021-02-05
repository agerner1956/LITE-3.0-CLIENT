/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Rest WS Get Authentication Token from LifeImage Rest WS Server Interface
*/

namespace Lite.Core.Interfaces.RestWS
{
    public interface ILiteRestWsToken
    {
        string GetToken(string userName, string password);
    }
}