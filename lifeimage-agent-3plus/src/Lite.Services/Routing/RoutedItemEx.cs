using Dicom;
using Dicom.Network;
using Lite.Core.Models;
using System;

namespace Lite.Services
{
    public class RoutedItemEx : RoutedItem
    {
        [NonSerialized()]
        public DicomFile sourceDicomFile;

        [NonSerialized()]
        public DicomFile destDicomFile;

        [NonSerialized()]
        public DicomRequest dicomRequest;
    }
}
