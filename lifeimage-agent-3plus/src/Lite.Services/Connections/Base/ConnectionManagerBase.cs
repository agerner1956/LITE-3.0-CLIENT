using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lite.Services.Connections
{
    public abstract class ConnectionManagerBase<T> : IConnectionManager where T : Connection
    {
        public Dictionary<string, bool> stores = new Dictionary<string, bool>();

        protected static object InitLock = new object();

        protected readonly ILogger _logger;
        protected readonly IUtil _util;

        protected ConnectionManagerBase(
            ILogger logger,            
            IUtil util)
        {
            _util = util;            
            _logger = logger;
        }

        public T Connection { get; internal set; }


        public void Load(Connection connection)
        {
            Connection = (T)connection;
            ProcessImpl(connection);
            Init();
        }

        protected abstract void ProcessImpl(Connection connection);

        public abstract Task Kickoff(int taskID);

        public abstract RoutedItem Route(RoutedItem routedItem, bool copy = false);

        public virtual void Init()
        {
        }

        public abstract void Stop();
    }
}
