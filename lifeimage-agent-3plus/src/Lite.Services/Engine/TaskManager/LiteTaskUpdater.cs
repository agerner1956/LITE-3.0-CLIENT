using Lite.Core;
using Lite.Core.Guard;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services
{
    public interface ILiteTaskUpdater
    {
        Task ProcessTempTasks(LITETask lITETask, List<LiteTaskInfo> tempTasks);
        Task CheckLongRunningTasks(LITETask liteTask, KeyValuePair<string, SemaphoreSlim> sem, List<LiteTaskInfo> tempTasks);
    }

    public sealed class LiteTaskUpdater : ILiteTaskUpdater
    {
        private readonly ILogger _logger;
        private readonly IProfileStorage _profileStorage;

        public LiteTaskUpdater(
            IProfileStorage profileStorage,
            ILogger<LiteTaskUpdater> logger)
        {
            _profileStorage = profileStorage;
            _logger = logger;
        }

        public async Task ProcessTempTasks(LITETask lITETask, List<LiteTaskInfo> tempTasks)
        {
            foreach (var task in tempTasks)
            {
                await ProcessTempTaskItem(lITETask, task);
            }
        }

        public async Task CheckLongRunningTasks(LITETask liteTask, KeyValuePair<string, SemaphoreSlim> sem, List<LiteTaskInfo> tempTasks)
        {
            foreach (var task in tempTasks.FindAll(e => e.type == sem.Key))
            {
                await CheckLongRunningTaskItem(liteTask, task);
            }
        }

        private async Task CheckLongRunningTaskItem(LITETask liteTask, LiteTaskInfo task)
        {
            Throw.IfNull(liteTask);
            Throw.IfNull(task);

            if (task.task == null)
            {
                return;
            }

            TaskStatus status = task.task.Status;

            switch (status)
            {
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                case TaskStatus.RanToCompletion:
                    {
                        task.MarkTaskComplete();  //this is in case taskCompletion didn't fire
                        await liteTask.RemoveTask(task);
                        break;
                    }
            }

            _logger.Log(LogLevel.Information, $" ->task: {task.taskID} {((task.task.Status.Equals(TaskStatus.WaitingForActivation)) ? "Running" : task.task.Status.ToString())} elapsed: {((task.completionDate == null) ? (DateTime.Now - task.bornOnDate) : (task.completionDate - task.bornOnDate))} {task.reference} {task.description}");
        }

        private async Task ProcessTempTaskItem(LITETask liteTask, LiteTaskInfo task)
        {
            if (task.task == null)
            {
                return;
            }

            //Report status on short running tasks
            if (task.isLongRunning) //long running tasks that manage mini-tasks
            {
                return;
            }

            switch (task.task.Status)
            {
                case TaskStatus.WaitingForActivation:

                case TaskStatus.Created:

                    if (DateTime.Now.Second % 10 == 0)
                    {
                        _logger.Log(LogLevel.Information, $"task: {task.taskID} {((task.task.Status.Equals(TaskStatus.WaitingForActivation)) ? "Running" : task.task.Status.ToString())} elapsed: {((task.completionDate == null) ? (DateTime.Now - task.bornOnDate) : (task.completionDate - task.bornOnDate))} {task.reference} {task.description}");
                    }

                    break;

                case TaskStatus.Running:

                case TaskStatus.WaitingForChildrenToComplete:

                case TaskStatus.WaitingToRun:

                    if (DateTime.Now.Second % 10 == 0)
                    {
                        _logger.Log(LogLevel.Information, $"task: {task.taskID} {((task.task.Status.Equals(TaskStatus.WaitingForActivation)) ? "Running" : task.task.Status.ToString())} elapsed: {((task.completionDate == null) ? (DateTime.Now - task.bornOnDate) : (task.completionDate - task.bornOnDate))} {task.reference} {task.description}");
                    }

                    break;

                case TaskStatus.Canceled:

                case TaskStatus.Faulted:

                case TaskStatus.RanToCompletion:

                    task.MarkTaskComplete();  //this is in case taskCompletion didn't fire

                    await liteTask.RemoveTask(task);

                    _logger.Log(LogLevel.Information, $"task: {task.taskID} {((task.task.Status.Equals(TaskStatus.WaitingForActivation)) ? "Running" : task.task.Status.ToString())} elapsed: {((task.completionDate == null) ? (DateTime.Now - task.bornOnDate) : (task.completionDate - task.bornOnDate))} {task.reference} {task.description}");

                    break;
            }

            var profile = _profileStorage.Current;
            if (DateTime.Now - task.bornOnDate > profile.maxTaskDuration)
            {
                _logger.Log(LogLevel.Information, $"task: {task.taskID} {((task.task.Status.Equals(TaskStatus.WaitingForActivation)) ? "Running" : task.task.Status.ToString())} elapsed: {((task.completionDate == null) ? (DateTime.Now - task.bornOnDate) : (task.completionDate - task.bornOnDate))} {task.reference} {task.description} RUNNING LONGER THAN profile.maxTaskDuration.");
                task.ctsPerTask.Cancel();
            }
        }
    }
}