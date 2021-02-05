using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    using Dicom;

    /// <summary>
    /// determines whether all tags specified in the rule are satisfied in the dataSet. 
    /// If a tagValue can be anything but must be present, use "+." regex.
    /// </summary>
    public interface IDoTagsMatchService
    {
        /// <summary>
        /// determines whether all tags specified in the rule are satisfied in the dataSet. 
        /// If a tagValue can be anything but must be present, use "+." regex.
        /// </summary>
        RoutedItem DoTagsMatch(DestRule rule, RoutedItem rr);
    }
    public sealed class DoTagsMatchService : IDoTagsMatchService
    {
        private readonly ILogger _logger;

        public DoTagsMatchService(ILogger<DoTagsMatchService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Determines whether all tags specified in the rule are satisfied in the dataSet. If a tagValue can be anything but must be present, use "+." regex
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="rr"></param>
        /// <returns></returns>
        public RoutedItem DoTagsMatch(DestRule rule, RoutedItem rr)
        {
            RoutedItemEx ri = (RoutedItemEx)rr;

            //dicom tag matching

            RoutedItemEx result = DicomMatching(ri, rule);
            if (result != null)
            {
                return result;
            }

            result = Hl7Matching(ri, rule);
            if (result != null)
            {
                return result;
            }

            ri.RuleMatch = true;
            return ri;
        }

        private RoutedItemEx DicomMatching(RoutedItemEx ri, DestRule rule)
        {
            var dataset = ri.sourceDicomFile?.Dataset;

            if (dataset == null)
            {
                return null;
            }

            foreach (Tag tag in rule.ruleTags)
            {
                DicomTag dtTag = DicomTag.Parse(tag.tag);

                if (dataset.Contains(dtTag)) //if tagvalue can be null it doesn't have to exist
                {
                    _logger.Log(LogLevel.Debug, $"tag {tag.tag} {dtTag} exists, now getting value...");

                    string tagValue = dataset.GetValue<string>(dtTag, 0);

                    ri.matches.AddRange(Regex.Matches(tagValue, tag.tagValue, RegexOptions.IgnoreCase));

                    if (ri.matches.Count == 0)
                    {
                        _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because tag.tagValue {tag.tagValue} !matches {tagValue}");

                        return ri;
                    }
                }
                else if (!tag.tagValue.Equals("\\0*"))
                {
                    _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because {dtTag} missing. Use \"\\0*\" for tagValue if you want to include missing and null tag values.");
                    return ri;
                }
            }

            _logger.Log(LogLevel.Debug, $"rule {rule.name} succeeds in Tag Match!");
            return null;
        }

        private RoutedItemEx Hl7Matching(RoutedItemEx ri, DestRule rule)
        {
            if (ri.sourceFileName == null || !ri.sourceFileName.EndsWith(".hl7"))
            {
                return null;
            }
            
            foreach (Tag tag in rule.ruleTags)
            {
                var segments = ri.hl7.FindAll(e => e.Key == tag.tag.Split(",")[0]);
                var elementNum = int.Parse(tag.tag.Split(",")[1]);

                foreach (var segment in segments)
                {
                    if (segment.Value.Count >= elementNum + 1)
                    {
                        var data = segment.Value[elementNum];

                        ri.matches.AddRange(Regex.Matches(data, tag.tagValue, RegexOptions.IgnoreCase));
                        if (ri.matches.Count == 0)
                        {
                            _logger.Log(LogLevel.Debug, $"rule {rule.name} fails this segment because data {data} !matches {tag.tagValue}");
                        }
                    }
                }

                if (ri.matches.Count == 0)
                {
                    if (!tag.tagValue.Equals("\\0*"))
                    {
                        _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because {tag} missing. Use \"\\0*\" for tagValue if you want to include missing and null tag values.");
                        return ri;
                    }
                }

                _logger.Log(LogLevel.Debug, $"rule {rule.name} succeeds in Tag Match!");
            }

            return null;
        }
    }
}
