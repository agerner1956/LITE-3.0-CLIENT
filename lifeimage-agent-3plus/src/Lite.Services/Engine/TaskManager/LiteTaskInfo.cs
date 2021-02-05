using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class LiteTaskInfo
    {
        private static int taskIDCounter;

        public CancellationToken ct;
        public CancellationTokenSource ctsPerTask;

        public LiteTaskInfo(string reference, bool isLongRunning)
        {
            this.reference = reference;
            this.isLongRunning = isLongRunning;
        }

        public LiteTaskInfo(string description, string reference, bool isLongRunning)
        {
            taskID = NewTaskID();
            this.description = description;
            this.reference = reference;
            this.isLongRunning = isLongRunning;
            ctsPerTask = new CancellationTokenSource();
        }

        public LiteTaskInfo(int taskID, Task task, string type, string description, string reference, bool isLongRunning)
        {
            this.taskID = taskID;
            this.task = task;
            this.task.ConfigureAwait(false);
            this.type = type;
            this.description = description;
            this.reference = reference;
            this.isLongRunning = isLongRunning;
            this.bornOnDate = DateTime.Now;
            this.completionDate = null;
            ctsPerTask = new CancellationTokenSource();
        }

        public LiteTaskInfo(Action action, string description, string reference, bool isLongRunning)
        {
            this.taskID = NewTaskID();
            this.description = description;
            this.reference = reference;
            this.isLongRunning = isLongRunning;
            this.bornOnDate = DateTime.Now;
            this.completionDate = null;
            ctsPerTask = new CancellationTokenSource();
            this.ct = ctsPerTask.Token;
            this.action = action;
        }

        public int taskID { get; private set; }
        public Task task { get; internal set; }
        public string type { get; private set; }  //put the method in here with no other reference
        public string description { get; private set; }
        public DateTime bornOnDate { get; private set; }
        public DateTime? completionDate { get; private set; }
        public string reference { get; private set; }
        public Action action { get; private set; }
        public bool isLongRunning { get; private set; }

        public void MarkTaskComplete()
        {
            completionDate = DateTime.Now;
        }

        public override int GetHashCode()
        {
            return reference.GetHashCode();
        }

        public static int NewTaskID()
        {
            return Interlocked.Increment(ref taskIDCounter);
        }
    }
}