using Lite.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Dicom;
using Lite.Services;

namespace Lite.Core.Utils
{
    public sealed class DicomUtil : IDicomUtil
    {
        private readonly ILogger _logger;
        private readonly IDiskUtils _util;

        public DicomUtil(
            IDiskUtils util,
            ILogger<DicomUtil> logger)
        {
            _util = util;
            _logger = logger;
        }

        public bool IsDICOM(RoutedItem routedItem)
        {
            FileStream stream = null;
            try
            {
                if (File.Exists(routedItem.sourceFileName))
                {
                    //check if dicom per spec directly
                    stream = File.OpenRead(routedItem.sourceFileName);
                    byte[] bytes = new byte[132];
                    //stream.Seek(128, SeekOrigin.Begin);
                    var count = stream.Read(bytes, 0, 132);
                    string dicomPrefix = Encoding.ASCII.GetString(bytes);
                    if (dicomPrefix.EndsWith("DICM"))
                    {
                        _logger.Log(LogLevel.Debug, $"{routedItem.sourceFileName} is DICOM");
                        return true;
                    }
                    else
                    {
                        _logger.Log(LogLevel.Information, $"{routedItem.sourceFileName} is not DICOM");
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
        }

        public RoutedItem Dicomize(RoutedItem ri)
        {
            var routedItem = (RoutedItemEx)ri;
            try
            {
                _logger.Log(LogLevel.Information, $"Attempting to DICOMIZE {routedItem.sourceFileName}");
                DicomDataset data = new DicomDataset();
                data.AddOrUpdate(DicomTag.PatientID, routedItem.PatientID);
                data.AddOrUpdate(DicomTag.StudyInstanceUID, routedItem.Study);
                data.AddOrUpdate(DicomTag.AccessionNumber, routedItem.AccessionNumber);
                data.AddOrUpdate(DicomTag.SeriesInstanceUID, routedItem.AccessionNumber);
                data.AddOrUpdate(DicomTag.SOPInstanceUID, routedItem.Sop);
                data.AddOrUpdate(DicomTag.IssuerOfPatientID, routedItem.PatientIDIssuer);
                data.Add(DicomTag.SOPClassUID, DicomUID.EncapsulatedPDFStorage);
                byte[] fileData = _util.ReadBytesFromFile(routedItem.sourceFileName);
                if (fileData.Length > 0)
                {
                    data.Add(DicomTag.EncapsulatedDocument, fileData);

                    routedItem.sourceDicomFile = new DicomFile(data);
                    routedItem.sourceDicomFile.Save(routedItem.sourceFileName);
                }
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Critical, $"{routedItem.sourceFileName} cannot be DICOMIZED {e.Message} {e.StackTrace}");

            }

            return routedItem;
        }
    }
}
