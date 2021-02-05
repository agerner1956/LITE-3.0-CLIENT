using Lite.Core.Guard;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    /// <summary>
    /// RunPreProcessFromConnectionScripts is intended to be run once for the fromConnection before outbound processing.
    /// </summary>
    public interface IRunPreProcessFromConnectionScriptsService
    {
        /// <summary>
        /// RunPreProcessFromConnectionScripts is intended to be run once for the fromConnection before outbound processing.
        /// </summary>
        Task RunPreProcessFromConnectionScripts(Rules Item, RoutedItem routedItem);
    }

    public sealed class RunPreProcessFromConnectionScriptsService : IRunPreProcessFromConnectionScriptsService
    {
        private readonly IScriptService _scriptService;
        private readonly ILogger _logger;

        public RunPreProcessFromConnectionScriptsService(
            IScriptService scriptService,
            ILogger<RunPreProcessFromConnectionScriptsService> logger)
        {
            _scriptService = scriptService;
            _logger = logger;
        }

        /// <summary>
        /// RunPreProcessFromConnectionScripts is intended to be run once for the fromConnection before outbound processing.
        /// </summary>
        /// <param name="routedItem"></param>
        /// <param name="Item"></param>
        /// <returns></returns>
        public async Task RunPreProcessFromConnectionScripts(Rules Item, RoutedItem routedItem)
        {
            Throw.IfNull(Item);

            var taskInfo = GetTaskInfo(routedItem);

            try
            {
                await RunImpl(Item, routedItem, taskInfo);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }

        private async Task RunImpl(Rules Item, RoutedItem routedItem, string taskInfo)
        {
            //get destRules that match this connection and 
            var destRulesToProcess = Item.destRules.FindAll(e => e.fromConnectionName == routedItem.fromConnection);
            _logger.Log(LogLevel.Debug, $"{taskInfo} found {destRulesToProcess.Count} rules");

            //loop the rules
            foreach (DestRule destRule in destRulesToProcess)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} rule {destRule.name}");

                await ProcessDestRule(Item, routedItem, destRule, taskInfo);
            }
        }        

        private async Task ProcessDestRule(Rules Item, RoutedItem routedItem, DestRule destRule, string taskInfo)
        {
            Throw.IfNull(destRule);

            _logger.Log(LogLevel.Debug, $"{taskInfo} rule {destRule.name}");            

            //run the scripts for this rule
            foreach (var scriptname in destRule.preProcessFromConnectionScriptNames)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} script {scriptname}");

                await ProcessScripts(scriptname, Item.scripts, routedItem, destRule);
            }
        }

        private async Task ProcessScripts(string scriptname, List<Script> scripts, RoutedItem routedItem, DestRule destRule)
        {
            Throw.IfNull(destRule);

            var taskInfo = GetTaskInfo(routedItem);

            _logger.Log(LogLevel.Debug, $"{taskInfo} script {scriptname}");

            if (!scriptname.Equals("ruleTags"))
            {
                var script = scripts.Find(e => e.name == scriptname);
                _logger.Log(LogLevel.Debug, $"{taskInfo} running script {script.name}");
                await _scriptService.RunAsync(script, routedItem);
                return;
            }

            //run the scripts for the rule tags
            foreach (Tag ruleTag in destRule.ruleTags)
            {
                await ProcessScriptFromTag(ruleTag, routedItem, scripts);
            }
        }

        private string GetTaskInfo(RoutedItem routedItem)
        {
            var taskInfo = $"task: {routedItem.TaskID}";
            return taskInfo;
        }

        private async Task ProcessScriptFromTag(Tag ruleTag, RoutedItem routedItem, List<Script> scripts)
        {
            Throw.IfNull(ruleTag);
            Throw.IfNull(scripts);
            Throw.IfNull(routedItem);

            var taskInfo = GetTaskInfo(routedItem);

            var tagscript = scripts.Find(e => e.name == ruleTag.scriptName);
            routedItem.ruleDicomTag = ruleTag;
            _logger.Log(LogLevel.Debug, $"{taskInfo} running rule tag: {ruleTag.tagName} tag script: {ruleTag.scriptName}");
            if (tagscript != null)
            {
                await _scriptService.RunAsync(tagscript, routedItem);
                //await tagscript.RunAsync(routedItem);
            }
            else
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} script: {ruleTag.scriptName} not found!");
            }
        }
    }
}
