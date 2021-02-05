namespace Lite.Core.Models
{
    /// <summary>
    /// CommandSet holds operating system commands for execution.  It comprises a fileName to execute and arguments to pass along.
    /// </summary>
    public class CommandSet
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }

        public CommandSet(string filename, string arguments)
        {
            FileName = filename;
            Arguments = arguments;
        }
    }
}
