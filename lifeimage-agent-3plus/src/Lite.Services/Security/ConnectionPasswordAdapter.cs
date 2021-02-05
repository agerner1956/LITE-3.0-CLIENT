using Lite.Core.Connections;
using Lite.Core.Guard;
using System;

namespace Lite.Core.Security
{
    public class ConnectionPasswordAdapter
    {
        private readonly ICrypto _crypto;
        public ConnectionPasswordAdapter(ICrypto crypto)
        {
            _crypto = crypto;
        }

        public void SetPassword(Connection connection, string value)
        {
            Throw.IfNull(connection);

            if(string.IsNullOrEmpty(value))
            //if (value != null && !value.Equals(""))
            {
                connection.password = _crypto.Protect(value);
                connection.sharedKey = Convert.ToBase64String(_crypto.Key);
                connection.IV = Convert.ToBase64String(_crypto.IV);
            }
            else
            {
                connection.password = null;
                connection.sharedKey = null;
                connection.IV = null;
            }
        }
    }
}
