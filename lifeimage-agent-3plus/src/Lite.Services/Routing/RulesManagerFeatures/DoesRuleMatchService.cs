using Dicom;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

namespace Lite.Services.Routing.RulesManagerFeatures
{
    public interface IDoesRuleMatchService
    {
        bool DoesRuleMatch(RoutedItem rr, DestRule rule);
    }

    public sealed class DoesRuleMatchService : IDoesRuleMatchService
    {
        private readonly IDoTagsMatchService _doTagsMatchService;
        private readonly ILogger _logger;

        public DoesRuleMatchService(
            IDoTagsMatchService doTagsMatchService,
            ILogger<DoesRuleMatchService> logger)
        {
            _doTagsMatchService = doTagsMatchService;
            _logger = logger;
        }

        public bool DoesRuleMatch(RoutedItem rr, DestRule rule)
        {
            RoutedItemEx routedItem = (RoutedItemEx)rr;

            //        Connection source, DestRule rule, DicomDataset dataSet
            //routedItem.sourceDicomFile?.Dataset
            //2017-10-09 sbh I was running into "hard to debug" npe so I broke out the logic for easier debugging
            //The idea here is to fail fast on rules and return false
            //If a rule field is specified it is "engaged" and must be satisfied
            //If a rule field is not specified ie null then it does not have to be satisfied

            //near future:  We might like to introspect any rule class to make this more dynamic
            //all we need is a fieldmatch table between hl7 and DICOM and other sources
            //then we can introspect class or JSON and loop the specifiers and compare to match
            //otherwise the Rule class itself can grow ridiculously large.

            try
            {
                return DoesRuleMatchImpl(routedItem, rule);
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }

            return false;
        }

        private bool DoesRuleMatchImpl(RoutedItemEx routedItem, DestRule rule)
        {
            //rules requiring DICOM dataset               
            if (routedItem.sourceDicomFile?.Dataset != null)
            {
                if (rule.modality != null)
                {
                    if (routedItem.sourceDicomFile.Dataset.Contains(DicomTag.Modality))
                    {
                        if (Regex.Matches(routedItem.sourceDicomFile.Dataset.GetValue<string>(DicomTag.Modality, 0), rule.modality, RegexOptions.IgnoreCase).Count == 0)
                        {
                            _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because modality {rule.modality} !matches {routedItem.sourceDicomFile.Dataset.GetValue<string>(DicomTag.Modality, 0)}");
                            return false;
                        }
                    }
                    else
                    {
                        _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because DicomTag.Modality missing.");
                        return false;
                    }
                }

                if (rule.referringPhysician != null)
                {
                    if (routedItem.sourceDicomFile.Dataset.Contains(DicomTag.ReferringPhysicianName))
                    {
                        if (Regex.Matches(rule.referringPhysician, routedItem.sourceDicomFile.Dataset.GetValue<string>(DicomTag.ReferringPhysicianName, 0), RegexOptions.IgnoreCase).Count == 0)
                        {
                            _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because referringPhysician {rule.referringPhysician} !matches {routedItem.sourceDicomFile.Dataset.GetValue<string>(DicomTag.ReferringPhysicianName, 0)}");
                            return false;
                        }
                    }
                    else
                    {
                        _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because DicomTag.ReferringPhysicianName missing.");
                        return false;
                    }
                }
            }

            if (rule.ruleTags != null && rule.ruleTags.Count > 0)
            {
                routedItem = (RoutedItemEx)_doTagsMatchService.DoTagsMatch(rule, routedItem);
                if (!routedItem.RuleMatch)
                {
                    _logger.Log(LogLevel.Debug, $"rule {rule.name} fails because Tags {rule.ruleTags} !matches {routedItem.sourceDicomFile.Dataset}");
                    return false;
                }
            }

            _logger.Log(LogLevel.Debug, $"rule {rule.name} id: {routedItem.id} succeeds!");
            return true;
        }
    }
}
