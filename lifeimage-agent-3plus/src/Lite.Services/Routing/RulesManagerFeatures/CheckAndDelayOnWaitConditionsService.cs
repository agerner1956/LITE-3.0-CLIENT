using Dicom;
using Lite.Core;
using Lite.Core.Enums;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    public interface ICheckAndDelayOnWaitConditionsService
    {
        Task<Priority> CheckAndDelayOnWaitConditions(RoutedItem ri);
    }

    public sealed class CheckAndDelayOnWaitConditionsService : ICheckAndDelayOnWaitConditionsService
    {
        private readonly IUtil _util;
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly ILogger _logger;

        public CheckAndDelayOnWaitConditionsService(
            IUtil util,
            IProfileStorage profileStorage,
            ILITETask taskManager,
            ILogger<CheckAndDelayOnWaitConditionsService> logger)
        {
            _util = util;
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _logger = logger;
        }

        public async Task<Priority> CheckAndDelayOnWaitConditions(RoutedItem ri)
        {
            RoutedItemEx routedItem = (RoutedItemEx)ri;
            var taskInfo = $"task: {routedItem.TaskID}";

            /*
        check and delay on wait conditions
            DICOM TAG (0000,0700)
            LOW = 0002H
            MEDIUM = 0000H
            HIGH = 0001H
        */
            //Engage: Waits get engaged when DICOM Priority Tag detected, and get disengaged when done
            ushort priority = 3;

            try
            {
                if (routedItem.sourceDicomFile != null)
                {
                    DicomDataset dataSet = routedItem.sourceDicomFile.Dataset;

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

                    Profile currentProfile = _profileStorage.Current;

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
                        _logger.Log(LogLevel.Information, $"{taskInfo} {uuid} has priority {priority}.");
                        if (priority.Equals(0x01))
                        {
                            currentProfile.highWait = true;
                            _logger.Log(LogLevel.Information, $"{taskInfo} {uuid} with high priority detected.  Setting highWait flag.");
                        }

                        if (priority.Equals(0x00))
                        {
                            currentProfile.mediumWait = true;
                            _logger.Log(LogLevel.Information, $"{taskInfo} {uuid} with medium priority detected.  Setting highWait flag.");
                        }
                    }

                    //Wait on Condition:
                    if (currentProfile.highWait || currentProfile.mediumWait)
                    { //something important is in mid transfer so check and wait if med or low
                        if (priority < 3 || priority.Equals(0x02))
                        {
                            //low or no priority is subject to both highWait and mediumWait conditions
                            if (currentProfile.highWait && !priority.Equals(0x01))
                            {
                                _logger.Log(LogLevel.Information, $"{taskInfo} highWait causing {currentProfile.highWaitDelay}ms delay for DICOM {uuid} in thread:{System.Threading.Thread.CurrentThread}.");
                                await Task.Delay(currentProfile.highWaitDelay, _taskManager.cts.Token).ConfigureAwait(false);
                            }
                            if (currentProfile.mediumWait && !priority.Equals(0x00) && !priority.Equals(0x01))
                            {
                                _logger.Log(LogLevel.Information, $"{taskInfo} mediumWait causing {currentProfile.mediumWaitDelay}ms delay for DICOM {uuid} in thread:{System.Threading.Thread.CurrentThread}.");
                                await Task.Delay(currentProfile.mediumWaitDelay, _taskManager.cts.Token).ConfigureAwait(false);
                            }
                        }
                        else if (priority.Equals(0x00))
                        {
                            //medium priority is subject to only highWait conditions
                            if (currentProfile.highWait)
                            {
                                _logger.Log(LogLevel.Information, $"{taskInfo} highWait causing {currentProfile.highWaitDelay}ms delay for DICOM {uuid} in thread:{System.Threading.Thread.CurrentThread}.");
                                await Task.Delay(currentProfile.highWaitDelay, _taskManager.cts.Token).ConfigureAwait(false);
                            }
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
            return _util.GetPriority(priority);
        }
    }
}
