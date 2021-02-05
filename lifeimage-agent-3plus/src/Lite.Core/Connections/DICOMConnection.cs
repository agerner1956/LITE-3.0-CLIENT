using Lite.Core.Enums;
using Lite.Core.Json;
using Lite.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading;

namespace Lite.Core.Connections
{
    public sealed class DICOMConnection : Connection
    {
        [JsonPropertyOrder(-90)]
        [JsonPropertyName("localAETitle")]
        public string localAETitle { get; set; }

        [JsonPropertyOrder(-90)]
        [JsonPropertyName("remoteAETitle")]
        public string remoteAETitle { get; set; }

        [JsonPropertyName("Linger")]
        public int Linger { get; set; } = 50;

        [JsonPropertyName("asyncInvoked")]
        public int asyncInvoked { get; set; } = 5;

        [JsonPropertyName("asyncPerformed")]
        public int asyncPerformed { get; set; } = 5;

        [JsonPropertyName("IgnoreSslPolicyErrors")]
        public bool IgnoreSslPolicyErrors { get; set; } = false;

        [JsonPropertyName("IgnoreUnsupportedTransferSyntaxChange")]
        public bool IgnoreUnsupportedTransferSyntaxChange { get; set; } = false;

        [JsonPropertyName("LogDataPDUs")]
        public bool LogDataPDUs { get; set; } = false;

        [JsonPropertyName("LogDimseDatasets")]
        public bool LogDimseDatasets { get; set; } = false;

        [JsonPropertyName("MaxClientsAllowed")]
        public int MaxClientsAllowed { get; set; } = 0;

        [JsonPropertyName("MaxCommandBuffer")]
        public uint MaxCommandBuffer { get; set; } = 1024;

        [JsonPropertyName("MaxDataBuffer")]
        public uint MaxDataBuffer { get; set; } = 1048576;

        [JsonPropertyName("MaxPDVsPerPDU")]
        public int MaxPDVsPerPDU { get; set; } = 0;

        [JsonPropertyName("TcpNoDelay")]
        public bool TcpNoDelay { get; set; } = true;

        [JsonPropertyName("UseRemoteAEForLogName")]
        public bool UseRemoteAEForLogName { get; set; } = false;

        [JsonPropertyName("maxRequestsPerAssociation")]
        public int maxRequestsPerAssociation { get; set; } = 1;

        [JsonPropertyName("msToWaitAfterSendBeforeRelease")]
        public int msToWaitAfterSendBeforeRelease { get; set; } = 50;

        /**
         cFindDICOMCharacterSet is used to add the specified character set to (0008,0005) during cFind Operations and
         is the character set used to convert the cFind parameters received, represented internally
         in c# as UTF-16 into the desired encoding.  Due to the mismatch of DICOM Encoding names that have corresponding
         equivalent aliases in c#, the conversion is described here.

         https://github.com/fo-dicom/fo-dicom/blob/17331dcb0c3aa3bbaf82236b26b735a0861a7cd0/DICOM/DicomEncoding.cs


         The ISO 10646-1, 10646-2, and their associated supplements and extensions correspond to the Unicode version 3.2 character set. 
         The ISO IR 192 corresponds to the use of the UTF-8 encoding for this character set.

         For example, when querying the dcmtk utility dcmqrscp without specifying 
         
         cFindCharacterSet = "ISO_IR 100", 
         
         dcmqrscp returns 
         
         W: Character set conversion is not available, comparing values that use different (incompatible) 
         character sets: "ASCII" and "ISO_IR 100".

         NOTE: for MIME / IANA	ISO-8859-1 the Alias(es) are iso-ir-100, csISOLatin1, latin1, l1, IBM819, CP819
         This is now standard behavior in the HTML5 specification, which requires that documents advertised as ISO-8859-1 
         actually be parsed with the Windows-1252 encoding.[1]
         ISO-8859-1 is the IANA preferred name for this standard when supplemented with the C0 and C1 control codes from 
         ISO/IEC 6429. The following other aliases are registered: iso-ir-100, csISOLatin1, latin1, l1, IBM819. Code page 
         28591 a.k.a. Windows-28591 is used for it in Windows.[3] IBM calls it code page 819 or CP819. Oracle calls it WE8ISO8859P1.[4]
         
         For more information see https://en.wikipedia.org/wiki/ISO/IEC_8859-1 .
         For a list of possible values see http://dicom.nema.org/medical/dicom/2016d/output/chtml/part02/sect_D.6.2.html
         For detailed DICOM encoding see http://dicom.nema.org/dicom/2013/output/chtml/part05/chapter_6.html
         */
        //public string cFindCharacterSet = DicomEncoding.Default.EncodingName;

        /// <summary>
        ///  List of dicom operations the connection wil handle. Values should be cFind, cMove or cStore
        /// </summary>
        [JsonPropertyName("dicomOperations")]
        public List<string> dicomOperations { get; set; } = new List<string>();

        [NonSerialized()]
        public ObservableCollection<RoutedItem> toDicom = new ObservableCollection<RoutedItem>();

        //[NonSerialized()]
        //public static List<IDicomServer> dicomListeners = new List<IDicomServer>();

        [NonSerialized()]
        private readonly SemaphoreSlim toDicomSignal = new SemaphoreSlim(0, 1);

        public List<string> AcceptedImageTransferSyntaxUIDs { get; set; } = new List<string>();
        public List<string> PossibleImageTransferSyntaxUIDs { get; set; } = new List<string>();

        public DICOMConnection()
        {
            connType = ConnectionType.dicom;
        }
    }
}
