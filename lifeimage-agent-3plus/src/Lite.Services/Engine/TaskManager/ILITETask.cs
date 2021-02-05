using Lite.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services
{
    public interface ILITETask
    {
        CancellationTokenSource cts { get; }

        void Start();
        Task<bool> Start(int taskID, Task task, string type, bool isLongRunning);
        Task<bool> Start(int taskID, Task task, string type, string reference, bool isLongRunning);
        void Stop(bool shutdown = false);
        void Stop(string key);

        bool CanStart(string key);        
        Task<int> CountByReference(string reference);             
        LiteTaskInfo Create(string description, string reference, bool isLongRunning);
        void CreateAndRun(Action action, string description, string reference, bool isLongRunning);
        void CreateAndRun(int taskID, Task task, string type, string description, string reference, bool isLongRunning);
        Task<Task[]> FindByReference(string reference);
        Task<Task[]> FindByType(string type);
        Task<LiteTaskInfo> GetTask(int taskID);
        void Init(string reference, bool isLongRunning);
        void Init(string description, string reference, bool isLongRunning);
        void MarkTaskComplete();
        void Register(string key, int parallelism);
        Task TaskCompletion(Profile profile);
        Task UpdateStatus();

        int NewTaskID();
    }
}