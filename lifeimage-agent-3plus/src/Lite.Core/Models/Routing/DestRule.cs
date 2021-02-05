using Lite.Core.Json;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lite.Core.Models
{
    /// <summary>
    /// DestRule contains all rule types defined to determine the destination for an inbound object.
    /// </summary>
    public class DestRule : IComparable<DestRule>
    {
        public DestRule()
        {
            toConnections = new List<ConnectionSet>();
        }

        /// <summary>
        /// Setting the order to -100 puts it ahead of all fields that are unassigned which are set to -1
        /// The name of the rule, to organize your thoughts and to help make the logging meaningful.
        /// </summary>
        [JsonPropertyOrder(-100)]
        [JsonPropertyName("name")]
        public string name { get; set; }


        /// <summary>
        /// A rule that is disabled will not be processed at all.  This allows for rule creation and  
        /// subsequent enablement/disablement for feature rollouts, problem isolation, etc.
        /// </summary>
        /// <remarks>
        /// True if rule is enabled, false to disable
        /// </remarks>
        [JsonPropertyOrder(-99)]
        [JsonPropertyName("enabled")]
        public bool enabled { get; set; } = true;

        /// <summary>
        ///  Rule types. Primary one is route, used for routing objects.
        /// </summary>
        public enum RuleType { FixedRoute, AddressableRoute, Notification }

        public RuleType Type { get; set; } = RuleType.FixedRoute;

        [NonSerialized()]
        public MatchCollection matches = null;

        [NonSerialized()]
        public bool RuleMatch = false;

        /// <summary>
        /// The name of the connection identified as the source for this rule.
        /// Source connection name
        /// </summary>
        [JsonPropertyOrder(-98)]
        [JsonPropertyName("fromConnectionName")]
        public string fromConnectionName { get; set; }

        /// The name of the connection identified as the destination for this rule.
        /// // Destination connection name
        [JsonPropertyOrder(-97)]
        [JsonPropertyName("toConnections")]
        public List<ConnectionSet> toConnections { get; set; }

        /// <summary>
        /// Modality filter. If this is populated only the modality specified will cause a match.
        /// </summary>
        /// <remarks>
        /// Modality to test against, null will ignored field      
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        //[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("modality")]
        public string modality { get; set; }

        /// <summary>
        /// Physician filter.  If this is populated only the physician specified will cause a match.
        /// Referring physician to test against, null will ignored field. 
        /// </summary>
        //[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("referringPhysician")]
        public string referringPhysician { get; set; }

        /// A list of Tags that will be used to determine a match. See Tag for details. All Tags must return true to match.
        public List<Tag> ruleTags = new List<Tag>();    // List of tags and values to test against

        /// <summary>
        /// A list of script names to execute once for the fromConnectionName for each inbound stream before streaming to toConnectionNames.
        /// </summary>
        [JsonPropertyName("preProcessFromConnectionScriptNames")]
        public List<string> preProcessFromConnectionScriptNames { get; set; } = new List<string>();

        /// <summary>
        /// A list of script names to execute once for the fromConnectionName for each inbound stream after 
        /// streaming to toConnectionNames.
        /// </summary>
        [JsonPropertyName("postProcessFromConnectionScriptNames")]
        public List<string> postProcessFromConnectionScriptNames { get; set; } = new List<string>();

        /// <summary>
        /// A list of script names to execute once for each toConnectionName for each outbound stream before  streaming.
        /// </summary>
        [JsonPropertyName("preProcessToConnectionScriptNames")]
        public List<string> preProcessToConnectionScriptNames { get; set; } = new List<string>();

        /// <summary>
        /// A list of script names to execute once for each toConnectionName for each outbound stream after streaming.
        /// </summary>
        [JsonPropertyName("preProcessToConnectionScriptNames")]
        public List<string> postProcessToConnectionScriptNames { get; set; } = new List<string>();

        int IComparable<DestRule>.CompareTo(DestRule other)
        {
            return fromConnectionName.CompareTo(other.fromConnectionName);
        }

        public bool IsSimple()
        {
            if (modality == null
           && postProcessFromConnectionScriptNames.Count == 0
           && postProcessToConnectionScriptNames.Count == 0
           && preProcessFromConnectionScriptNames.Count == 0
           && preProcessToConnectionScriptNames.Count == 0
           && referringPhysician == null
           && ruleTags.Count == 0)
            {
                return true;
            }
            return false;
        }
    }
}
