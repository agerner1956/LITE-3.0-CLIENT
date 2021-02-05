using Lite.Core.Guard;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    public interface IRulesEvalService
    {
        List<ConnectionSet> Eval(Rules Item, RoutedItem routedItem);
    }

    public sealed class RulesEvalService : IRulesEvalService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IDoesRuleMatchService _doesRuleMatchService;
        private readonly ILogger _logger;

        public RulesEvalService(
            IProfileStorage profileStorage,
            IDoesRuleMatchService doesRuleMatchService,
            ILogger<RulesEvalService> logger)
        {
            _profileStorage = profileStorage;
            _doesRuleMatchService = doesRuleMatchService;
            _logger = logger;
        }

        /// <summary>
        /// Evaluate the rule sets to see which ones match the from connection and send on to the toConnections.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public List<ConnectionSet> Eval(Rules Item, RoutedItem routedItem)
        {
            Throw.IfNull(Item);
            var taskInfo = $"task: {routedItem.TaskID} id: {routedItem.id}";

            var connList = new List<ConnectionSet>();

            if (routedItem.toConnections.Count > 0)
            {
                //we've already been told where to send the request, just return it
                _logger.Log(LogLevel.Debug, $"{taskInfo} toConnections already populated, skipping eval.");

                return routedItem.toConnections;
            }
            else
            {
                foreach (var destRule in Item.destRules.FindAll(e => e.fromConnectionName == routedItem.fromConnection && e.enabled == true))
                {
                    //2018-08-07 shb BOUR-630 check the destination connections to see if they are enabled.  If there is no enabled destination then don't add the connection set
                    int enabledDest = 0;
                    foreach (var dest in destRule.toConnections)
                    {
                        enabledDest += _profileStorage.Current.connections.FindAll(e => e.name == dest.connectionName && e.enabled == true).Count;
                    }
                    if (enabledDest > 0)
                    {
                        if (_doesRuleMatchService.DoesRuleMatch(routedItem, destRule))
                        {
                            _logger.Log(LogLevel.Debug, $"{taskInfo} evaluates to {destRule.toConnections.Count} connections.");

                            connList.AddRange(destRule.toConnections);
                            if (destRule.Type == DestRule.RuleType.AddressableRoute)
                            {
                                foreach (var con in connList)
                                {
                                    //amend the connlist sharing destintations with matches
                                    foreach (var match in destRule.matches)
                                    {
                                        ShareDestinations sd = new ShareDestinations
                                        {
                                            boxUuid = match.ToString()
                                        };
                                        con.shareDestinations.Add(sd);
                                    }
                                }
                            }
                        }
                    }
                }

                if (connList.Count == 0)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} There are no routes for {routedItem.fromConnection}.");
                }
            }

            //do we need to flatten in case of duplicates?

            // foreach (var conn in profile.connections.FindAll(e => e.enabled == true))
            // {
            //     if (conn.enabled == true)
            //     {
            //         foreach (var connectionSet in connListNames)
            //         {
            //             if (conn.name.Equals(connectionSet.connectionName))
            //             {
            //                 if(Logger.logger.FileTraceLevel == "Verbose") _logger.Log(LogLevel.Debug, $"conn {conn.name} succeeds because == {connectionSet.connectionName}");
            //                 connList.Add(connectionSet);
            //             }
            //             else
            //             { 

            //                 if(Logger.logger.FileTraceLevel == "Verbose") _logger.Log(LogLevel.Debug, $"conn {conn.name} fails because != {connectionSet.connectionName}");

            //             }
            //         }
            //     }
            // }

            //for request/response mechanism route to connections that can provide this functionality
            //trim the connList to only those connections that  support request/response
            if (routedItem.type == RoutedItem.Type.RPC)
            {
                foreach (var conn in connList.ToArray())
                {
                    if (_profileStorage.Current.connections.FindAll(e => e.name == conn.connectionName && e.enabled && e.requestResponseEnabled).Count == 0)
                    {
                        _logger.Log(LogLevel.Debug, $"{conn.connectionName} is being removed from destinations because requestResponseEnabled: false");
                        connList.Remove(conn);
                    }
                }
            }

            return connList;
        }
    }
}
