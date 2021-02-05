using Dicom;
using Lite.Core.Guard;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lite.Services.Routing.RouteItemManager
{
    public class TranslateServiceParams
    {
        public TranslateServiceParams(DicomFile sourceDicomFile, string tagString, string translationFileName, bool force)
        {
            this.tagString = tagString;
            this.sourceDicomFile = sourceDicomFile;
            this.translationFileName = translationFileName;
            this.force = force;
        }

        public string tagString { get; private set; }
        public string translationFileName { get; private set; }
        public bool force { get; set; }
        public DicomFile sourceDicomFile { get; private set; }
    }

    public interface ITranslateService
    {
        void Translate(RoutedItem Item, TranslateServiceParams @params);
    }

    public sealed class TranslateService : ITranslateService
    {
        private readonly ILogger _logger;
        public TranslateService(ILogger<TranslateService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Look up translation for specified tag, write translationhistory record and replace tag contents.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="params"></param>
        public void Translate(RoutedItem Item, TranslateServiceParams @params)
        {
            Throw.IfNull(@params);
            Throw.IfNull(@params);

            try
            {
                TranslateImpl(Item, @params);
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
        }

        private void TranslateImpl(RoutedItem Item, TranslateServiceParams @params)
        {
            var tagString = @params.tagString;
            var translationFileName = @params.translationFileName;
            var force = @params.force;
            var sourceDicomFile = @params.sourceDicomFile;

            var taskInfo = $"task: {Item.TaskID} fromConnection: {Item.fromConnection} tag: {tagString} translationFileName: {translationFileName} force: {force}";
            Translation translation = null;

            DicomTag tag = DicomTag.Parse(tagString);
            DicomTag studyTag = DicomTag.Parse("0020,0010");
            string json = null;
            string from = null;
            string to = null;

            //load the translation specified
            try
            {
                json = File.ReadAllText($"translations/{translationFileName}.json");
            }
            catch (Exception)
            {
                _logger.Log(LogLevel.Error, $"{translationFileName}.json doesn't exist, creating default one.");
                //create a default one
                Translation t = new Translation
                {
                    Tag = "0000,0000",
                    FromTo = new Dictionary<string, string>() { { "fromvalue1", "tovalue1" }, { "fromvalue2", "tovalue2" } }
                };
                json = JsonSerializer.Serialize(t);
                Directory.CreateDirectory("translations");
                File.WriteAllText($"translations/{translationFileName}.json", json);
            }

            if (string.IsNullOrEmpty(json))
            {
                _logger.Log(LogLevel.Error, $"{translationFileName}.json is null or blank.");
            }
            else
            {
                translation = JsonSerializer.Deserialize<Translation>(json);
            }

            if (!sourceDicomFile.Dataset.Any())
            {
                return;
            }

            //need to swich based on VR to get correct data type, for now just string
            if (sourceDicomFile.Dataset.TryGetSingleValue<string>(tag, out from))
            {
                if (from == null)
                {
                    from = "NULL";
                }

                if (from == "")
                {
                    from = "EMPTY";
                }

                if (translation.FromTo.TryGetValue(from, out to))
                {
                    sourceDicomFile.Dataset.AddOrUpdate<string>(tag, to);
                }
                else if (force)
                {
                    to = System.Guid.NewGuid().ToString();
                    translation.FromTo.Add(from, to);
                    //sourceDicomFile.Dataset.AddOrUpdate<string>(tag, "MissingTranslation");
                    var j = JsonSerializer.Serialize(translation);
                    Directory.CreateDirectory("translations");
                    File.WriteAllText($"translations/{translationFileName}.json", j);
                }
            }
            else if (force)
            {
                from = "MissingTag";
                to = "MissingTag";
                sourceDicomFile.Dataset.AddOrUpdate<string>(tag, to);
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} from: {from} to: {to}");

            //write history
            TranslationHistory history = new TranslationHistory();
            if (!sourceDicomFile.Dataset.TryGetSingleValue(studyTag, out string study))
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Translation History requires a study \"0020,0010\" tag");
            }
            else
            {
                if (from == null)
                {
                    from = "NULL";
                }

                if (from == "")
                {
                    from = "EMPTY";
                }

                history.Study = study;

                _logger.Log(LogLevel.Debug, $"{taskInfo} Writing history for study: {study} tagString: {tagString} from: {from} to: {to}");

                history.Translations.Add(new Translation { Tag = tagString, FromTo = new Dictionary<string, string>() { { from, to } } });

                var historyjson = JsonSerializer.Serialize(history);
                _logger.Log(LogLevel.Debug, $"{taskInfo} Translation History to file: translations/{history.Study}.json json: {historyjson}");

                Directory.CreateDirectory("translations/history");

                using StreamWriter sw = File.AppendText($"translations/history/{history.Study}.json");
                sw.WriteLine(historyjson);
            }
        }
    }
}
