using System;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    /// <summary>
    /// Scripts are written in c# and may perform advanced logic, call out to external programs, and participate in enterprise workflow processing.For example, a script could check the time of day and day of week.It could make a REST call to see who is on-call.It could make an API call to TIBCO for 
    /// advanced rule processing and integration.In other words, the sky is the limit and the intention is to not place any restrictions on the type of code allowable, but individual organzations may place their own restrictions.For details, see the topic TODO: Writing Scripts.
    /// </summary>
    public class Script
    {
        /// <summary>
        /// name of the script is used in rules to define which scripts are run, and for ease of referral.
        /// </summary>
        [JsonPropertyName("name")]
        public string name { get; set; }

        /// <summary>
        /// source is the source code of the script, the same as any c# code.
        /// </summary>
        [JsonPropertyName("source")]
        public string source { get; set; }

        [JsonPropertyName("imports")]
        public string[] imports { get; set; } = { "" };

        [JsonPropertyName("references")]
        public string[] references { get; set; } = { "System.Object" };

        /// <summary>
        /// script is the compiled script object.
        /// </summary>
        [NonSerialized]
        public Microsoft.CodeAnalysis.Scripting.Script script;

        [JsonPropertyName("errors")]
        public string errors { get; set; }
    }
}
