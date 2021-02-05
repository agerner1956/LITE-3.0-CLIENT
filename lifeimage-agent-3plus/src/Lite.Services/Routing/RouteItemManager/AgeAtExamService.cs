using Dicom;
using Lite.Core.Guard;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;

namespace Lite.Services.Routing.RouteItemManager
{
    public class AgeAtExamServiceParams
    {
        public AgeAtExamServiceParams(DicomFile sourceDicom, Tag ruleTag)
        {
            sourceDicomFile = sourceDicom;
            ruleDicomTag = ruleTag;
        }

        public DicomFile sourceDicomFile { get; private set; }
        public Tag ruleDicomTag { get; private set; }
    }

    public interface IAgeAtExamService
    {
        /// <summary>
        /// Convenience method for AgeAtExam script to calculate the age at exam and update the specified tag in the Rule.
        /// </summary>
        void AgeAtExam(AgeAtExamServiceParams @params, string taskInfo);
    }

    public sealed class AgeAtExamService : IAgeAtExamService
    {
        private readonly ILogger _logger;

        public AgeAtExamService(ILogger<AgeAtExamService> logger)
        {
            _logger = logger;
        }

        public void AgeAtExam(AgeAtExamServiceParams @params, string taskInfo)
        {
            Throw.IfNull(@params);
            AgeExamImpl(@params, taskInfo);
        }

        private void AgeExamImpl(AgeAtExamServiceParams @params, string taskInfo)
        {
            var sourceDicomFile = @params.sourceDicomFile;
            var ruleDicomTag = @params.ruleDicomTag;

            _logger.Log(LogLevel.Debug, $"{taskInfo} Calculating AgeAtExam.");
            _logger.Log(LogLevel.Debug, $"{taskInfo} tag: {ruleDicomTag.tag} value: {ruleDicomTag.tagValue}");

            CultureInfo culture;
            DateTimeStyles styles;

            DateTime birthdate;
            DateTime examdate;

            DicomTag birthdateTag = DicomTag.Parse("0010,0030");
            DicomTag examdateTag = DicomTag.Parse("0008,0020");
            DicomTag tag = DicomTag.Parse(ruleDicomTag.tag);

            if (!sourceDicomFile.Dataset.Contains(birthdateTag) || !sourceDicomFile.Dataset.Contains(examdateTag))
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Required tags not present to perform this script!");
            }

            string birthdateValue = sourceDicomFile.Dataset.GetValue<string>(birthdateTag, 0);
            string examdateValue = sourceDicomFile.Dataset.GetValue<string>(examdateTag, 0);

            string tagValue = null;

            sourceDicomFile.Dataset.TryGetSingleValue<string>(tag, out tagValue);
            _logger.Log(LogLevel.Debug, $"{taskInfo} Patient's Age: {tagValue} birthdate:{birthdateValue} examdate:{examdateValue}");

            culture = CultureInfo.CreateSpecificCulture("en-US");
            styles = DateTimeStyles.AssumeLocal;
            birthdate = DateTime.ParseExact(birthdateValue, "yyyyMMdd", culture, styles);
            examdate = DateTime.ParseExact(examdateValue, "yyyyMMdd", culture, styles);
            _logger.Log(LogLevel.Debug, $"{taskInfo} birthdate:{birthdate} examdate:{examdate}");
            string newAge = null;

            //perform the calculations

            int newAgeDays = (examdate - birthdate).Days;

            double newAgeWeeks = (examdate - birthdate).TotalDays / 7;

            double newAgeMonths = Math.Abs((examdate.Month - birthdate.Month) + 12 * (examdate.Year - birthdate.Year));

            int newAgeYears = examdate.Year - birthdate.Year;

            if (birthdate > examdate.AddYears(-newAgeYears))
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Subtract a Year: birthdate > examdate.AddYears(-newAgeYears)");
                newAgeYears--;
            }

            if (newAgeDays / 365 >= 2)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Choosing Years: newAgeDays/365 >=2");

                newAge = $"{newAgeYears.ToString("d3")}Y";
            }
            else if (newAgeDays / 30 >= 1)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Choosing Months: newAgeDays/30 >= 1");

                _logger.Log(LogLevel.Debug, $"{taskInfo} newAgeMonths: {newAgeMonths}");
                newAge = $"{(int)newAgeMonths:d3}M";
            }
            else if (newAgeDays / 7 >= 1)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Choosing Weeks: newAgeDays/7 >=1");
                newAge = $"{(int)newAgeWeeks:d3}W";
            }
            else
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Choosing Days");
                newAge = $"{(examdate - birthdate).Days:d3}D";
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} Before {tagValue}");

            _logger.Log(LogLevel.Debug, $"{taskInfo} NewAgeDays: {newAgeDays} NewAgeWeeks: {newAgeWeeks} newAgeMonths: {newAgeMonths} newAgeYears: {newAgeYears} newAge: {newAge}");

            sourceDicomFile.Dataset.AddOrUpdate<string>(tag, newAge);

            _logger.Log(LogLevel.Debug, $"{taskInfo} RuleTag: {ruleDicomTag.tag} {ruleDicomTag.tagValue} DicomTag: {tag.DictionaryEntry.Name} newValue: {newAge}");
        }
    }
}
