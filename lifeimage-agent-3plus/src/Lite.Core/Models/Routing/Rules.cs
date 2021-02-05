using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    /// <summary>
    /// The outer containment for all things Rules.  Rules contains lists of rules by type, which are generally assigned to a Profile.
    /// Rules also contains code for processing events against the Rules defined.In general, rule processing returns true or false. Future
    /// Rules may return values used in processing, like a list of Connections to use, or a notification list.
    /// </summary>
    public sealed class Rules
    {
        /// <summary>
        /// List of rules.
        /// </summary>
        [JsonPropertyName("destRules")]
        public List<DestRule> destRules = new List<DestRule>();

        [JsonPropertyName("scripts")]
        public List<Script> scripts = new List<Script>();
    }
}
