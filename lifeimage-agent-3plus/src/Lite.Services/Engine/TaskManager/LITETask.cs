using Lite.Core;
using Lite.Services.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LITETask : ILITETask
    {
        private static readonly SemaphoreSlim taskLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// List of all LITETasks including contextual workitems and pointer to async Task.  Note: static List is thread safe.
        /// </summary>
        private static readonly List<LiteTaskInfo> tasks = new List<LiteTaskInfo>();

        private static bool fastStatus = false;

        public static Dictionary<string, SemaphoreSlim> Parallelism = new Dictionary<string, SemaphoreSlim>();

        /// <summary>
        /// This is the global cancellation token source to be used when we want everything to shut down or reinitialize.  
        /// It should not be used if we are only trying to cancel a single task, loop or call.
        /// </summary>
        public static CancellationTokenSource _cts = new CancellationTokenSource();

        private LiteTaskInfo _taskInfo;

        public static bool _shutdown;

        private readonly IConnectionManagerFactory _connectionManagerFactory;
        private readonly IProfileStorage _profileStorage;
        private readonly ILiteTaskUpdater _liteTaskUpdater;
        private readonly ILogger _logger;

        public LITETask(
            IConnectionManagerFactory connectionManagerFactory,
            IProfileStorage profileStorage,
            ILiteTaskUpdater liteTaskUpdater,
            ILogger<LITETask> logger)
        {
            _connectionManagerFactory = connectionManagerFactory;
            _profileStorage = profileStorage;
            _liteTaskUpdater = liteTaskUpdater;
            _logger = logger;
        }

        public CancellationTokenSource cts
        {
            get { return _cts; }
        }

        public int NewTaskID()
        {
            return LiteTaskInfo.NewTaskID();
        }

        public void Init(string reference, bool isLongRunning)
        {
            _taskInfo = new LiteTaskInfo(reference, isLongRunning);
        }

        public void Init(string description, string reference, bool isLongRunning)
        {
            _taskInfo = new LiteTaskInfo(description, reference, isLongRunning)
            {
                ctsPerTask = new CancellationTokenSource()
            };
        }

        public void CreateAndRun(int taskID, Task task, string type, string description, string reference, bool isLongRunning)
        {
            try
            {
                _taskInfo = new LiteTaskInfo(taskID, task, type, description, reference, isLongRunning)
                {
                    ct = cts.Token,
                    ctsPerTask = new CancellationTokenSource()
                };

                AddTask(_taskInfo).Wait();
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Could not create and run task");
            }
        }

        public void CreateAndRun(Action action, string description, string reference, bool isLongRunning)
        {
            try
            {
                _taskInfo = new LiteTaskInfo(action, description, reference, isLongRunning)
                {
                    ctsPerTask = new CancellationTokenSource()
                };

                _taskInfo.ct = _taskInfo.ctsPerTask.Token;
                AddTask(_taskInfo).Wait();

                _taskInfo.task = Task.Factory.StartNew(action, _taskInfo.ct, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "CreateAndRun failed");
            }
        }

        private async Task AddTask(LiteTaskInfo liteTask)
        {
            try
            {
                bool success = await taskLock.WaitAsync(60000, cts.Token).ConfigureAwait(false);
                tasks.Add(liteTask);
            }
            catch (TaskCanceledException e)
            {
                _logger.Log(LogLevel.Information, $"{e.Message}");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                try
                {
                    if (taskLock.CurrentCount == 0)
                    {
                        taskLock.Release();
                    }
                }
                catch (Exception) { }
            }
        }

        internal async Task RemoveTask(LiteTaskInfo liteTask)
        {
            _logger.Log(LogLevel.Debug, $"Removing task: {liteTask.taskID} reference: {liteTask.reference} description: {liteTask.description}");

            try
            {
                bool success = await taskLock.WaitAsync(60000, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
                try
                {
                    if (taskLock.CurrentCount == 0)
                    {
                        taskLock.Release();
                    }
                }
                catch (Exception) { }
            }

            try
            {
                var result = tasks.Remove(liteTask);
                // if (!result)
                // {
                //     if (Logger.logger.FileTraceLevel == "Verbose") _logger.Log(LogLevel.Verbose, $"Unable to remove task: {liteTask.taskID} reference: {liteTask.reference} description: {liteTask.description}");
                // }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            finally
            {
            }
        }

        private async Task<Task[]> GetShortRunningAsyncTasks()
        {
            try
            {
                if (tasks != null && tasks.Count > 0 && cts != null && taskLock != null)
                {
                    bool success = await taskLock.WaitAsync(60000, cts.Token).ConfigureAwait(false);
                    return tasks.FindAll(e => e.isLongRunning == false && e.task != null).Select(e => e.task).ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "GetShortRunningAsyncTasks");
                return null;
            }
            finally
            {
                try
                {
                    if (taskLock.CurrentCount == 0)
                    {
                        taskLock.Release();
                    }
                }
                catch (Exception) { }
            }
        }

        public LiteTaskInfo Create(string description, string reference, bool isLongRunning)
        {
            LiteTaskInfo task = new LiteTaskInfo(description, reference, isLongRunning);

            try
            {
                AddTask(task).Wait();
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            return task;
        }

        public override bool Equals(object obj)
        {
            // All we are checking here is the reference field
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            LiteTaskInfo d = (LiteTaskInfo)obj;

            return (_taskInfo.reference == d.reference);
        }

        /// <summary>
        /// Mark the task complete.
        /// </summary>
        /// <param name="taskID"></param>
        /// <returns></returns>
        public async Task<LiteTaskInfo> GetTask(int taskID)
        {
            try
            {
                foreach (var task in await GetLITETasks().ConfigureAwait(false))
                {
                    if (task.taskID == taskID)
                    {
                        return task;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);

            }
            return null;
        }

        /// <summary>
        /// stop all running tasks by sending a cancellation token and waiting.
        /// </summary>
        /// <param name="shutdown"></param>
        public void Stop(bool shutdown = false)
        {
            try
            {
                if (shutdown)
                {
                    _shutdown = shutdown;
                }

                var profile = _profileStorage.Current;
              
                foreach (var conn in profile.connections)
                {
                    //if (conn.connType == Connection.ConnectionType.hl7 && Profile.SkipProfileMergeHL7 == true) continue;
                    //if (conn.connType == Connection.ConnectionType.hl7 && LITE.SkipShutdown == true) continue;

                    var manager = _connectionManagerFactory.GetManager(conn);
                    _logger.Log(LogLevel.Information, $"stopping {conn.name}");
                    manager.Stop();
                }

                _logger.Log(LogLevel.Information, $"Stopping {tasks.Count} Tasks...");
                cts.Cancel();

                while (tasks.Count > 1)
                {
                    fastStatus = true;
                    _logger.Log(LogLevel.Information, $"Waiting for {tasks.Count} tasks to stop...");
                    Task.Delay(1000).Wait();
                    UpdateStatus().Wait();
                }

                bool success = taskLock.WaitAsync(60000).Result;
            }
            catch (Exception e)
            {                
                _logger.LogFullException(e);
            }
        }

        /// <summary>
        /// enable running tasks by setting a new cancellation token source.
        /// </summary>
        public void Start()
        {
            try
            {
                if (!_shutdown)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        _cts = new CancellationTokenSource();
                    }

                    if (taskLock.CurrentCount == 0)
                    {
                        taskLock.Release();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }

        /// <summary>
        /// mark the task complete.
        /// </summary>
        public void MarkTaskComplete()
        {
            _taskInfo.MarkTaskComplete();            
        }

        public override int GetHashCode()
        {
            return _taskInfo.GetHashCode();
        }

        public async Task<int> CountByRegex(string pattern)
        {
            int matches = 0;

            try
            {
                foreach (var task in await GetLITETasks().ConfigureAwait(false))
                {
                    if (task != null)
                    {
                        matches += Regex.Matches(task.reference, pattern).Count;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Critical, $"Returning matches = 1000 due to exception {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                }
                matches = 1000;
            }

            _logger.Log(LogLevel.Debug, $"{matches} matches that match {pattern}");

            return matches;
        }

        public async Task<Task[]> FindByReference(string reference)
        {
            return (await GetLITETasks().ConfigureAwait(false)).FindAll(e => e.reference == reference).Select(e => e.task).ToArray();
        }

        public async Task<Task[]> FindByType(string type)
        {
            return (await GetLITETasks().ConfigureAwait(false)).FindAll(e => e.type == type).Select(e => e.task).ToArray();
        }

        public async Task<int> CountByReference(string reference)
        {
            _logger.Log(LogLevel.Debug, $"Searching for reference: {reference}");

            try
            {
                return (await GetLITETasks().ConfigureAwait(false)).FindAll(e => e.reference == reference).Count;
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"Returning matches = 1000 due to exception");
                return 1000;
            }
        }

        public async Task<int> CountByType(string type)
        {
            _logger.Log(LogLevel.Debug, $"Searching for type: {type}");

            try
            {
                return (await GetLITETasks().ConfigureAwait(false)).FindAll(e => e.type == type).Count;
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Critical, $"Returning matches = 1000 due to exception {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                }
                return 1000;
            }
        }

        public async Task UpdateStatus()
        {
            _logger.Log(LogLevel.Debug, $"Entering updateStatus");
            try
            {
                while (true)
                {
                    Profile profile = _profileStorage.Current;
                    try
                    {
                        //Are there any short running tasks?  If so, switch fastStatus
                        fastStatus = false;

                        List<LiteTaskInfo> tempTasks = null;

                        if (cts.IsCancellationRequested)
                        {
                            tempTasks = tasks.ToList();
                        }
                        else
                        {
                            tempTasks = await GetLITETasks().ConfigureAwait(false);
                        }

                        int i = 0;

                        await _liteTaskUpdater.ProcessTempTasks(this, tempTasks);                        

                        //Report status on long running tasks
                        if (!LITETask.fastStatus)
                        {
                            foreach (var sem in Parallelism.ToArray())
                            {
                                _logger.Log(LogLevel.Information, $"Semaphore: {++i} Avail: {sem.Value.CurrentCount} {sem.Key}");

                                await _liteTaskUpdater.CheckLongRunningTasks(this, sem, tempTasks);
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Log(LogLevel.Information, $"Task was canceled.");
                    }
                    catch (InvalidOperationException)
                    {
                        _logger.Log(LogLevel.Debug, $"Task collection was modified.");
                        // tasks was updated so exit

                    }
                    catch (Exception e)
                    {
                        _logger.LogFullException(e);
                        //throw e;
                    }

                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    if (fastStatus | LITETask._cts.IsCancellationRequested)
                    {
                        await Task.Delay(profile.taskDelay, LITETask._cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(profile.KickOffInterval, _cts.Token).ConfigureAwait(false);
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
            finally
            {
                try
                {
                    Parallelism.TryGetValue($"UpdateStatus", out SemaphoreSlim sem);
                    if (sem.CurrentCount == 0)
                    {
                        Stop($"UpdateStatus");
                    }
                }
                catch (Exception) { }
            }
        }

        public async Task TaskCompletion(Profile profile)
        {
            var taskInfo = "";
            try
            {
                while (true)
                {
                    Task[] arrayOfTasks = null;
                    LiteTaskInfo task = null;
                    arrayOfTasks = await GetShortRunningAsyncTasks().ConfigureAwait(false);
                    if (arrayOfTasks != null && arrayOfTasks.Length > 0)
                    {
                        var completedTask = await Task.WhenAny(arrayOfTasks);

                        if (completedTask != null)
                        {
                            try
                            {
                                task = (await GetLITETasks().ConfigureAwait(false)).Find(e => e.task == completedTask);
                            }
                            catch (Exception e)
                            {
                                _logger.LogFullException(e, taskInfo);
                            }
                        }

                        if (task != null && task.task != null)
                        {
                            task.MarkTaskComplete();
                            _logger.Log(LogLevel.Information, $"{taskInfo} task: {task?.taskID} {((task.task.Status.Equals(TaskStatus.WaitingForActivation)) ? "Running" : task.task.Status.ToString())} elapsed: {((task.completionDate == null) ? (DateTime.Now - task.bornOnDate) : (task.completionDate - task.bornOnDate))} {task.reference} {task.description}");
                            await RemoveTask(task);
                        }
                    }
                    else
                    {
                        //var profile = _profileStorage.Current;
                        await Task.Delay(profile.taskDelay, LITETask._cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                Stop($"TaskCompletion");
            }
        }

        public void Register(string key, int parallelism)
        {
            Parallelism.TryGetValue(key, out SemaphoreSlim sem);
            if (sem == null)
            {
                sem = new SemaphoreSlim(parallelism, parallelism);
                Parallelism.Add(key, sem);
                _logger.Log(LogLevel.Information, $"{key} is registered for parallelism of initial: {parallelism} maximum: {parallelism}");
            }
        }

        public bool CanStart(string key)
        {
            Parallelism.TryGetValue(key, out SemaphoreSlim sem);
            if (sem == null)
            {
                sem = new SemaphoreSlim(1, 1);
                Parallelism.Add(key, sem);
                _logger.Log(LogLevel.Information, $"{key} is registered for parallelism of initial: 1 maximum: 1");
            }
            if (sem.CurrentCount > 0) return true;
            return false;
        }

        public async Task<bool> Start(int taskID, Task task, string type, bool isLongRunning)
        {
            bool success = false;

            try
            {
                CreateAndRun(taskID, task, type, type, type, isLongRunning);

                Parallelism.TryGetValue(type, out SemaphoreSlim sem);
                if (sem == null)
                {
                    sem = new SemaphoreSlim(1, 1);
                    Parallelism.Add(type, sem);
                    _logger.Log(LogLevel.Information, $"{type} is registered for parallelism of initial: 1 maximum: 1");
                }
                success = await sem.WaitAsync(-1, LITETask._cts.Token).ConfigureAwait(false);

                if (success && task.Status == TaskStatus.Created)
                {
                    _logger.Log(LogLevel.Debug, $"{type} starting, {sem.CurrentCount} semaphores remaining.");
                    task.Start();
                }
                else
                {
                    _logger.Log(LogLevel.Error, $"{type} NOT starting, {sem.CurrentCount} semaphores remaining.");
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

            return success;
        }

        public async Task<bool> Start(int taskID, Task task, string type, string reference, bool isLongRunning)
        {
            bool success = false;

            try
            {
                CreateAndRun(taskID, task, type, type, reference, isLongRunning);

                Parallelism.TryGetValue(type, out SemaphoreSlim sem);
                if (sem == null)
                {
                    sem = new SemaphoreSlim(1, 1);
                    Parallelism.Add(type, sem);
                    _logger.Log(LogLevel.Information, $"{type} is registered for parallelism of initial: 1 maximum: 1");
                }
                success = await sem.WaitAsync(-1, LITETask._cts.Token).ConfigureAwait(false);

                if (success && task.Status == TaskStatus.Created)
                {
                    _logger.Log(LogLevel.Debug, $"{type} starting, {sem.CurrentCount} semaphores remaining.");
                    task.Start();
                }
                else
                {
                    _logger.Log(LogLevel.Error, $"{type} NOT starting, {sem.CurrentCount} semaphores remaining.");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"TaskID: {taskID} type: {type} reference: {reference} was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
            }

            return success;
        }

        public void Stop(string key)
        {
            SemaphoreSlim sem = null;
            try
            {
                Parallelism.TryGetValue(key, out sem);
                if (sem != null)
                {
                    sem.Release();
                }
            }
            catch (SemaphoreFullException)
            {
                _logger.Log(LogLevel.Information, $"Semaphore {key} already released");
            }
            catch (Exception e)
            {
                string msg = $"{key} current: ";
                if (sem != null)
                {
                    msg += $"{sem.CurrentCount}";
                }
                else
                {
                    msg += "NULL";
                }

                _logger.LogFullException(e, msg);
            }
        }

        private async Task Run(Action action, TimeSpan period)
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    await Task.Delay(period, _cts.Token).ConfigureAwait(false);

                    if (!_cts.IsCancellationRequested)
                        action();
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

        private async Task<int> CountByDescription(string description)
        {
            try
            {
                return (await GetLITETasks().ConfigureAwait(false)).FindAll(e => e.description == description).Count;
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Critical, $"Returning matches = 1000 due to exception {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                }
                return 1000;
            }
        }

        private async Task<List<LiteTaskInfo>> GetLITETasks()
        {
            try
            {
                bool success;
                if (cts.IsCancellationRequested)
                {
                    success = await taskLock.WaitAsync(1000).ConfigureAwait(false);

                }
                success = await taskLock.WaitAsync(-1, cts.Token).ConfigureAwait(false);
                return tasks.ToList();
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);

                // don't reset stack trace!
                throw;
                //throw e;
            }
            finally
            {
                try
                {
                    if (taskLock.CurrentCount == 0)
                    {
                        taskLock.Release();
                    }
                }
                catch (Exception) { }
            }
        }
    }
}