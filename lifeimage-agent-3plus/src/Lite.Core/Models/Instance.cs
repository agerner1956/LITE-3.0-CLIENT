using System;

namespace Lite.Core.Models
{
    public class Instance
    {
        public string number { get; set; }
        public string uid { get; set; }
        public string sopClass { get; set; }
        public string type { get; set; }

        [NonSerialized]
        public Extension3 extension;
        public DateTime downloadStarted { get; set; }
        public DateTime downloadCompleted { get; set; }
        public int attempts { get; set; }
    }
}
