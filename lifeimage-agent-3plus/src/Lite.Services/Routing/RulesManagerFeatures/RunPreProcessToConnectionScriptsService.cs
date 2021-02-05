using Lite.Core.Guard;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    /// <summary>
    /// RunPreProcessToConnectionScripts is intended to be run once for the stream before outbound processing.
    /// </summary>
    public interface IRunPreProcessToConnectionScriptsService
    {
        /// <summary>
        /// RunPreProcessToConnectionScripts is intended to be run once for the stream before outbound processing.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        Task RunPreProcessToConnectionScripts(Rules Item, RoutedItem routedItem);
    }

    public sealed class RunPreProcessToConnectionScriptsService : IRunPreProcessToConnectionScriptsService
    {
        private readonly IScriptService _scriptService;
        private readonly ILogger _logger;

        public RunPreProcessToConnectionScriptsService(
            IScriptService scriptService,
            ILogger<RunPreProcessToConnectionScriptsService> logger)
        {
            _scriptService = scriptService;
            _logger = logger;
        }

        /// <summary>
        /// RunPreProcessToConnectionScripts is intended to be run once for the stream before outbound processing
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public async Task RunPreProcessToConnectionScripts(Rules Item, RoutedItem routedItem)
        {
            Throw.IfNull(Item);
            Throw.IfNull(routedItem);

            try
            {
                await RunImpl(Item, routedItem);
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

        private async Task RunImpl(Rules Item, RoutedItem routedItem)
        {            
            var taskInfo = $"task: {routedItem.TaskID}";

            //get destRules that match this connection and 
            var destRulesToProcess = Item.destRules.FindAll(e => e.fromConnectionName == routedItem.fromConnection);

            //loop the rules
            foreach (var destRule in destRulesToProcess)
            {
                //run the scripts for this rule
                foreach (var scriptname in destRule.preProcessToConnectionScriptNames)
                {
                    if (scriptname.Equals("ruleTags"))
                    {
                        //run the scripts for the rule tags
                        foreach (var ruleTag in destRule.ruleTags)
                        {
                            var tagscript = Item.scripts.Find(e => e.name == ruleTag.scriptName);
                            routedItem.ruleDicomTag = ruleTag;
                            _logger.Log(LogLevel.Debug, $"{taskInfo} running tag script {tagscript.name}");

                            await _scriptService.RunAsync(tagscript, routedItem);
                        }
                    }
                    else
                    {

                        var script = Item.scripts.Find(e => e.name == scriptname);
                        _logger.Log(LogLevel.Debug, $"{taskInfo} running tag script {script.name}");
                        //await script.RunAsync(routedItem);
                        await _scriptService.RunAsync(script, routedItem);
                    }

                }
            }
        }
    }
}
