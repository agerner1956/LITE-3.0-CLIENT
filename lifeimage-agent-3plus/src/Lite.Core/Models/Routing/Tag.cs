using System.Text.Json.Serialization;

namespace Lite.Core.Models
{
    /// <summary>
    /// Rule Tag is used to define a rule against any dicom tag.
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// Dicom tag (required) in the form of xxxx,yyyy for example "0020,000d".
        /// </summary>
        [JsonPropertyName("tag")]
        public string tag { get; set; }

        /// <summary>
        /// Dicom tag name (optional)
        /// </summary>
        [JsonPropertyName("tagName")]
        public string tagName { get; set; }

        /// <summary>
        /// TagValue is the value to compare against.  Regex can be used here.
        /// </summary>
        [JsonPropertyName("tagValue")]
        public string tagValue { get; set; }         // Value to compare against

        /**
            scriptName allows processing in-flight tagValues during transfers
            Common industry terminology might include de-identify, data augmentation, percentile ranking, etc.
            RandomizeString | ConvertToGuid | IncrementByOne are some de-identification (morph) script ideas.
            PopulateRank | Notify | MachineLearningLayer are some data augmentation ideas.
            The scope is a specific tag, so it will execute once for this tag, but during pre/post processing
            against a fromConnection (once) or the list of toConnections (once for each toConnection).

            In comparison, a general script may execute for every RoutedItem and you can build what you
            need there to perform the same sort of work.  Or you can define the script more granularly to execute 
            whenever a tag exists, or a tag of a given tagValue exists. 

            Tag level scripting allows for easier tag specific scripts, while still having access to the entire
            RoutedItem object.

            IMPORTANT!!  To execute rule-level scripts, decide where the execution occurs pre/post and 
            fromConnection/toConnection and enter the special scriptName "ruleTags" in the desired step.

            Example:  Note "ruleTags" is located in preProcessFromConnectionScriptNames so the ruleTags
            scripts (de-identify Patient's Name) will be executed once before any further processing.

            "ruleTags": [
             {
              "tag": "0010,0010",
              "tagValue": ".+",
              "scriptName": "RandomizeString"
            }
            ],
             "modality": null,
             "preProcessFromConnectionScriptNames": [
             "ruleTags",
             "ScheduleRouter",
             "HighPriority"
            ]
        */
        public string scriptName { get; set; }
    }
}
