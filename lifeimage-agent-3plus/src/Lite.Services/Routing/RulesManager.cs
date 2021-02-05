using Dicom;
using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Routing.RulesManagerFeatures;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services
{
    public class RulesManager : IRulesManager
    {
        private readonly ILogger _logger;
        private readonly IDiskUtils _util;
        private readonly IProfileStorage _profileStorage;
        private readonly IRulesEvalService _rulesEvalService;
        private readonly IRunPreProcessFromConnectionScriptsService _runPreProcessFromConnectionScriptsService;
        private readonly IRunPreProcessToConnectionScriptsService _runPreProcessToConnectionScriptsService;
        private readonly IRunPostProcessFromConnectionScriptsService _runPostProcessFromConnectionScriptsService;
        private readonly IRunPostProcessToConnectionScriptsService _runPostProcessToConnectionScriptsService;
        private readonly ICheckAndDelayOnWaitConditionsService _checkAndDelayOnWaitConditionsService;

        public RulesManager(
            IDiskUtils util,
            IProfileStorage profileStorage,
            IRulesEvalService rulesEvalService,
            IRunPreProcessFromConnectionScriptsService runPreProcessFromConnectionScriptsService,
            IRunPreProcessToConnectionScriptsService runPreProcessToConnectionScriptsService,
            IRunPostProcessFromConnectionScriptsService runPostProcessFromConnectionScriptsService,
            IRunPostProcessToConnectionScriptsService runPostProcessToConnectionScriptsService,
            ICheckAndDelayOnWaitConditionsService checkAndDelayOnWaitConditionsService,
            ILogger<RulesManager> logger)
        {
            _util = util;
            _rulesEvalService = rulesEvalService;
            _profileStorage = profileStorage;
            _runPreProcessFromConnectionScriptsService = runPreProcessFromConnectionScriptsService;
            _runPreProcessToConnectionScriptsService = runPreProcessToConnectionScriptsService;
            _runPostProcessFromConnectionScriptsService = runPostProcessFromConnectionScriptsService;
            _runPostProcessToConnectionScriptsService = runPostProcessToConnectionScriptsService;
            _checkAndDelayOnWaitConditionsService = checkAndDelayOnWaitConditionsService;
            _logger = logger;
        }

        public Rules Item { get; private set; }

        public void Init(Rules item)
        {
            Item = item;
        }

        /// <summary>
        /// Evaluate the rule sets to see which ones match the from connection and send on to the toConnections.
        /// </summary>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public List<ConnectionSet> Eval(RoutedItem routedItem)
        {
            return _rulesEvalService.Eval(Item, routedItem);
        }

        /// <summary>
        /// RunPreProcessFromConnectionScripts is intended to be run once for the fromConnection before outbound processing
        /// </summary>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public async Task RunPreProcessFromConnectionScripts(RoutedItem routedItem)
        {
            await _runPreProcessFromConnectionScriptsService.RunPreProcessFromConnectionScripts(Item, routedItem);
        }

        /// <summary>
        /// RunPostProcessFromConnectionScripts is intended to be run once for the fromConnection after outbound processing
        /// </summary>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public async Task RunPostProcessFromConnectionScripts(RoutedItem routedItem)
        {
            await _runPostProcessFromConnectionScriptsService.RunPostProcessFromConnectionScripts(Item, routedItem);
        }

        /// <summary>
        /// RunPreProcessToConnectionScripts is intended to be run once for the stream before outbound processing
        /// </summary>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public async Task RunPreProcessToConnectionScripts(RoutedItem routedItem)
        {
            await _runPreProcessToConnectionScriptsService.RunPreProcessToConnectionScripts(Item, routedItem);
        }

        /// <summary>
        /// RunPostProcessToConnectionScripts is intended to be run once for each toConnection after outbound processing
        /// </summary>
        /// <param name="routedItem"></param>
        /// <returns></returns>
        public async Task RunPostProcessToConnectionScripts(RoutedItem routedItem)
        {
            await _runPostProcessToConnectionScriptsService.RunPostProcessToConnectionScripts(Item, routedItem);
        }

        public async Task<Priority> CheckAndDelayOnWaitConditions(RoutedItem ri)
        {
            return await _checkAndDelayOnWaitConditionsService.CheckAndDelayOnWaitConditions(ri);
        }

        public void DisengageWaitConditions(RoutedItem rr)
        {
            RoutedItemEx routedItem = (RoutedItemEx)rr;
            var taskInfo = $"task: {routedItem.TaskID}";

            if (routedItem.sourceDicomFile != null)
            {
                DicomDataset dataSet = routedItem.sourceDicomFile.Dataset;

                //Disengage: Waits get engaged when DICOM Priority Tag detected, and get disengaged when done
                ushort priority = 3;
                string uuid = null;
                try
                {
                    if (dataSet.Contains(DicomTag.StudyInstanceUID))
                    {
                        uuid = dataSet.GetValue<string>(DicomTag.StudyInstanceUID, 0);
                    }
                }
                catch (DicomDataException e)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} {uuid} has no StudyInstanceUID field. {e.Message} {e.StackTrace}");
                }

                try
                {
                    if (dataSet.Contains(DicomTag.Priority))
                    {
                        priority = dataSet.GetValue<ushort>(DicomTag.Priority, 0);
                    }
                    else
                    {
                        _logger.Log(LogLevel.Debug, $"{taskInfo} {uuid} has no priority field.");
                    }
                }
                catch (DicomDataException e)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} {uuid} has no priority field. {e.Message} {e.StackTrace}");
                }

                if (priority < 3)
                {
                    if (dataSet.Contains(DicomTag.Priority))
                    {
                        if (dataSet.GetValue<ushort>(DicomTag.Priority, 0) == 0x01)
                        {
                            _logger.Log(LogLevel.Information, $"{taskInfo} {uuid} with high priority completed.  Clearing highWait flag.");
                            _profileStorage.Current.highWait = false;
                        }

                        if (dataSet.GetValue<ushort>(DicomTag.Priority, 0) == 0x00)
                        {
                            _logger.Log(LogLevel.Information, $"{taskInfo} {uuid} with medium priority completed.  Clearing mediumWait flag.");
                            _profileStorage.Current.mediumWait = false;
                        }
                    }
                }
            }
        }

        public async Task<RoutedItem> SendToRules(RoutedItem ri, IRoutedItemManager routedItemManager, IConnectionRoutedCacheManager connectionRoutedCacheManager)
        {
            var taskInfo = $"task: {ri.TaskID} {ri.fromConnection}";
            Priority priority = Priority.Low;

            //first things first, deep copy the routedItem so the sender can dequeue safely
            //RoutedItemEx routedItem = (RoutedItemEx)ri.Clone();

            routedItemManager.Init(ri);
            RoutedItemEx routedItem = (RoutedItemEx)routedItemManager.Clone();

            try
            {
                //Check rules and if simple, don't open or save dicom
                var destRulesToProcess = Item.destRules.FindAll(e => e.fromConnectionName == routedItem.fromConnection);
                bool simple = true;
                foreach (var destRule in destRulesToProcess)
                {
                    if (!destRule.IsSimple()) simple = false;
                }

                if (!simple)
                {
                    //This allows the sender to just provide a filename.
                    //Open the file and stream if file and stream are null and fileName is specified.  
                    //If the sender provides a stream and/or open DicomFile, then we need to close them
                    //when done. 
                    routedItemManager.Open();

                    //preProcessFromConnectionScriptNames executes once for each inbound stream before streaming out
                    //perceived problem with location of this call is that we already 
                    //have both file and stream and need to do tag morphing on file before streaming, or in the 
                    //middle of two streams so trying this in DicomListener instead of here.  Moving will require that 
                    //Connections implement script calls.
                    routedItem.rules = this.Item;
                    await RunPreProcessFromConnectionScripts(routedItem);

                    //scripts can modify tags and content.  We need to save the file before proceeding
                    string filePath = null;
                    if (routedItem.sourceDicomFile != null)
                    {

                        var dir = _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + "Rule.cs" + Path.DirectorySeparatorChar + "toStage2";
                        Directory.CreateDirectory(dir);
                        filePath = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid();
                        _logger.Log(LogLevel.Debug, $"{taskInfo} routedItem.sourceFileName: {routedItem.sourceFileName} is being saved to {filePath} after PreProcessFromConnectionScripts completion");
                        var oldfile = routedItem.sourceFileName;

                        if (!_util.IsDiskAvailable(routedItem.sourceDicomFile.File.Name, _profileStorage.Current))
                        {
                            throw new Exception($"Insufficient disk to write {filePath}");
                        }

                        if (routedItem.sourceDicomFile.File.Exists)
                        {
                            routedItem.sourceFileName = filePath;
                            routedItem.sourceDicomFile.Save(filePath);
                        }
                    }
                    else if (routedItem.sourceFileName.EndsWith(".hl7"))
                    {
                        //do nothing
                    }

                    routedItemManager.Close();
                    routedItemManager.Open();

                    routedItem.toConnections = Eval(routedItem);

                    //preProcessToConnectionScriptNames executes once for each outbound stream before streaming out
                    //moved this outside the toConnection loop to allow the toConnection array to be manipulated
                    await RunPreProcessToConnectionScripts(routedItem);
                    priority = await CheckAndDelayOnWaitConditions(routedItem);
                }
                else
                {
                    routedItem.toConnections = Eval(routedItem);
                }

                foreach (ConnectionSet toConnection in routedItem.toConnections)
                {
                    Connection toConn = _profileStorage.Current.connections.Find(e => e.name.Equals(toConnection.connectionName));
                    _logger.Log(LogLevel.Debug, $"{taskInfo} ToConnection Found, Sending to: {toConn.name}");


                    routedItemManager.Init(routedItem);
                    var clone = (RoutedItem)routedItemManager.Clone();

                    connectionRoutedCacheManager.Route(clone);
                    //toConn.Route();  //each receiver needs their own clone


                    //postProcessToConnectionScriptNames executes once for each outbound stream after streaming out
                    await RunPostProcessToConnectionScripts(routedItem);
                }

                if (!simple)
                {
                    DisengageWaitConditions(routedItem);
                    //postProcessFromConnectionScriptNames executes once for each inbound stream after streaming out
                    await RunPostProcessFromConnectionScripts(routedItem);
                }

                return routedItem;
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                // if (routedItem.sourceFileName != null)
                // {

                _logger.LogFullException(e);
                //                }

                throw;
            }
            finally
            {
                routedItemManager.Close();
                routedItem = null;
            }

            return routedItem;
        }

        /// <summary>
        /// Identify if any destinations exist for this source connection
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public bool DoesRouteDestinationExistForSource(string source)
        {
            RoutedItemEx routedItem = new RoutedItemEx
            {
                fromConnection = source
            };
            var destinations = Eval(routedItem);
            if (destinations.Count > 0) return true;
            return false;
        }
    }
}
