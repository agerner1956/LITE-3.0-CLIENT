using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;

namespace Lite.Services
{
    public class RouteItemHandler
    {
        protected readonly ILogger _logger;

        public RouteItemHandler(ILogger logger)
        {
            _logger = logger;
        }
        protected void WriteDetailedLog(Exception e, RoutedItem item, string taskInfo)
        {
            _logger.LogFullException(e, $"{taskInfo} routedItemMetaFile: {(item.RoutedItemMetaFile ?? "null")}");
        }
    }
}
