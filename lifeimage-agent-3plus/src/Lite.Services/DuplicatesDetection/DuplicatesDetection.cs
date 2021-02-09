using Dicom;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Lite.Services
{
    public interface IDuplicatesDetectionService
    {
        void DuplicatesPurge();
        bool DuplicatesReference(string name, string file);
        bool DuplicatesReference1(string name, string uniqueIdentifier);
    }

    public sealed class DuplicatesDetectionService : IDuplicatesDetectionService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILogger _logger;

        public DuplicatesDetectionService(
            IProfileStorage profileStorage,
            ILogger<DuplicatesDetectionService> logger)
        {
            _profileStorage = profileStorage;
            _logger = logger;
        }

        public string duplicatesPurgeTimeFile = "purgeTime"; // purge timestamp in sec

        public string duplicatesDirectoryPath
        {
            get
            {
                return _profileStorage.Current.tempPath + Path.DirectorySeparatorChar + "duplicates"; // duplicates detection directory
            }
        }

        public string duplicatesReferences = "references"; // duplicates reference drectory

        public void DuplicatesPurge()
        {
            double nowSeconds = DateTime.Now.Subtract(DateTime.MinValue).TotalSeconds;
            nowSeconds = Math.Round(nowSeconds, 0, MidpointRounding.ToEven);
            string duplicatesPurgeTimeFilePath =
                duplicatesDirectoryPath + Path.DirectorySeparatorChar + duplicatesPurgeTimeFile;
            FileInfo fi = new FileInfo(duplicatesPurgeTimeFilePath);
            if (fi.Exists)
            {
                double lastPurgeTimeSeconds = Convert.ToDouble(File.ReadAllText(duplicatesPurgeTimeFilePath));
                double diff = nowSeconds - lastPurgeTimeSeconds;
                if (diff > _profileStorage.Current.duplicatesDetectionInterval)
                {
                    string duplicatesDirectoryReferencesPath =
                        duplicatesDirectoryPath + Path.DirectorySeparatorChar + duplicatesReferences;
                    DuplicatesPurgeLoopThroughSubDirectories(duplicatesDirectoryReferencesPath, nowSeconds);
                    System.IO.File.WriteAllText(duplicatesPurgeTimeFilePath, "" + nowSeconds);
                }
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(duplicatesDirectoryPath);
                if (!fi.Exists)
                {
                    Directory.CreateDirectory(duplicatesDirectoryPath);
                }

                // create new duplicatesPurgeTimeFile
                using StreamWriter sw = File.CreateText(duplicatesPurgeTimeFilePath);
                sw.WriteLine("" + nowSeconds);
            }
        }

        private void DuplicatesPurgeLoopThroughSubDirectories(string path, double nowSeconds)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            if (di.Exists)
            {
                string[] subdirInDirectory = Directory.GetDirectories(path);
                foreach (string subdirPath in subdirInDirectory)
                {
                    DuplicatesPurgeLoopThroughSubDirectories(subdirPath, nowSeconds);
                }

                string[] filesInDirectory = Directory.GetFiles(path);
                foreach (string filePath in filesInDirectory)
                {
                    DateTime creation = File.GetCreationTime(@filePath);
                    double creationTimeSeconds = creation.Subtract(DateTime.MinValue).TotalSeconds;
                    creationTimeSeconds = Math.Round(creationTimeSeconds, 0, MidpointRounding.ToEven);
                    double diff = nowSeconds = creationTimeSeconds;
                    if (diff > _profileStorage.Current.duplicatesDetectionInterval)
                    {
                        File.Delete(filePath);
                    }
                }
            }
        }

        public bool DuplicatesReference(string name, string file)
        {
            FileInfo fi = new FileInfo(file);
            if (!fi.Exists)
            {
                _logger.Log(LogLevel.Critical, $"File: " + file + " referenced in queue does not exist.");
                return false;
            }
            string uniqueDicomCompoundIdentifier = uniqueDicomIdentifier(file);
            if (uniqueDicomCompoundIdentifier.Equals(""))
            {
                _logger.Log(LogLevel.Critical, $"File: " + file + " is of wrong type or corrupted. UniqueDicomIdentifier can't be generated.");
                //File.Delete(file);
                return false;
            }
            string referenceDirPath =
                duplicatesDirectoryPath + Path.DirectorySeparatorChar + duplicatesReferences +
                Path.DirectorySeparatorChar + name;
            string referenceFilePath = referenceDirPath + Path.DirectorySeparatorChar + uniqueDicomCompoundIdentifier;
            fi = new FileInfo(referenceFilePath);
            if (fi.Exists)
            {
                // if reference file exist in the duplicates directory it is duplicate and will be skipped when processing the queue
                string now = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                string message = "file with unique Identifier: " + uniqueDicomCompoundIdentifier + " is duplicate: eliminated " + now;
                _logger.Log(LogLevel.Information, $"{message}");
                //File.Delete(file);
                return false;
            }

            DirectoryInfo di = new DirectoryInfo(referenceDirPath);
            if (!di.Exists)
            {
                Directory.CreateDirectory(referenceDirPath);
            }

            // create new duplicate Reference File
            using (StreamWriter sw = File.CreateText(referenceFilePath))
            {
                sw.Write("");
            }

            return true;
        }

        public bool DuplicatesReference1(string name, string uniqueIdentifier)
        {
            // AMG 20200701 - Windows does not allow ':' in the file name.
            string fileName = uniqueIdentifier.Replace(":", "_");
            string referenceDirPath =
                duplicatesDirectoryPath + Path.DirectorySeparatorChar + duplicatesReferences +
                Path.DirectorySeparatorChar + name;
            string referenceFilePath = referenceDirPath + Path.DirectorySeparatorChar + fileName;
            FileInfo fi = new FileInfo(referenceFilePath);

            if (fi.Exists)
            {
                string uniqueIdentifierTemplate = File.ReadAllText(referenceFilePath);
                if (uniqueIdentifierTemplate.Equals(uniqueIdentifier))
                {
                    string now = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                    string message = "file " + name + " with unique identifier " + uniqueIdentifier +
                                     " is duplicate: eliminated " + now;
                    _logger.Log(LogLevel.Information, $"{message}");
                    return false;
                }
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(referenceDirPath);
                if (!di.Exists)
                {
                    Directory.CreateDirectory(referenceDirPath);
                }

                // create new duplicate Reference File
                using StreamWriter sw = File.CreateText(referenceFilePath);
                sw.Write(uniqueIdentifier);
            }

            return true;
        }

        private string uniqueDicomIdentifier(string path)
        {
            string uniqueDicomIdentifier;
            try
            {
                var file = DicomFile.Open(path);
                var dicomDataset = file.Dataset;
                var studyInstanceUID = dicomDataset.GetSingleValue<string>(DicomTag.StudyInstanceUID);
                var seriesInstanceUID = dicomDataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID);
                var sopClassUid = dicomDataset.GetSingleValue<string>(DicomTag.SOPClassUID);
                var sopInstanceUID = dicomDataset.GetSingleValue<string>(DicomTag.SOPInstanceUID);
                uniqueDicomIdentifier = studyInstanceUID + "_" + seriesInstanceUID + "_" + sopClassUid + "_" +
                                        sopInstanceUID;
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
                uniqueDicomIdentifier = string.Empty;
            }

            return uniqueDicomIdentifier;
        }
    }
}
