/*
   ### Developer: Alexander Gerner
   ### Initial Date: 01/28/2021
   ### Summary: Rest WS Client SendEmail Interface
*/

using Lite.Core.Models.Entities;

namespace Lite.Core.Interfaces.RestWS
{
    public interface ILiteEmail
    {
        string SendEmail(EmailEntity email);
    }
}