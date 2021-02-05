using Lite.Core.Guard;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    /// <summary>
    /// RunPostProcessToConnectionScripts is intended to be run once for each toConnection after outbound processing.
    /// </summary>
    public interface IRunPostProcessToConnectionScriptsService
    {
        /// <summary>
        /// RunPostProcessToConnectionScripts is intended to be run once for each toConnection after outbound processing.
        /// </summary>
        Task RunPostProcessToConnectionScripts(Rules Item, RoutedItem routedItem);
    }

    public sealed class RunPostProcessToConnectionScriptsService : IRunPostProcessToConnectionScriptsService
    {
        private readonly IScriptService _scriptService;
        private readonly ILogger _logger;

        public RunPostProcessToConnectionScriptsService(
            IScriptService scriptService,
            ILogger<RunPostProcessToConnectionScriptsService> logger)
        {
            _scriptService = scriptService;
            _logger = logger;
        }

        /// <summary>
        /// RunPostProcessToConnectionScripts is intended to be run once for each toConnection after outbound processing.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public async Task RunPostProcessToConnectionScripts(Rules Item, RoutedItem routedItem)
        {
            Throw.IfNull(Item);
            Throw.IfNull(routedItem);

            var taskInfo = $"task: {routedItem.TaskID}";

            try
            {
                //get destRules that match this connection and 
                var destRulesToProcess = Item.destRules.FindAll(e => e.fromConnectionName == routedItem.fromConnection);

                //loop the rules
                foreach (var destRule in destRulesToProcess)
                {
                    //run the scripts for this rule
                    foreach (var scriptname in destRule.postProcessToConnectionScriptNames)
                    {
                        if (scriptname.Equals("ruleTags"))
                        {
                            //run the scripts for the rule tags
                            foreach (var ruleTag in destRule.ruleTags)
                            {
                                var tagscript = Item.scripts.Find(e => e.name == ruleTag.scriptName);
                                routedItem.ruleDicomTag = ruleTag;
                                _logger.Log(LogLevel.Debug, $"{taskInfo} running tag script {tagscript.name}");
                                //await tagscript.RunAsync(routedItem);
                                await _scriptService.RunAsync(tagscript, routedItem);
                            }
                        }
                        else
                        {
                            var script = Item.scripts.Find(e => e.name == scriptname);
                            _logger.Log(LogLevel.Debug, $"{taskInfo} running script {script.name}");
                            //await script.RunAsync(routedItem);
                            await _scriptService.RunAsync(script, routedItem);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }
    }
}
