using Dicom;
using Dicom.Network;
using Lite.Core.Guard;
using Lite.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lite.Services.Routing.RouteItemManager
{
    public sealed class RoutedItemClonable
    {
        private readonly List<Tag> _ruleDicomTags;
        private readonly Tag _ruleDicomTag;
        private readonly Rules _rules;

        private readonly DicomFile _sourceDicomFile;
        private readonly DicomFile _destDicomFile;

        private readonly DicomRequest _dicomRequest;
        private readonly Stream _stream;

        public RoutedItemClonable(RoutedItemManager manager)
        {
            Throw.IfNull(manager);

            _ruleDicomTags = manager.ruleDicomTags;
            _ruleDicomTag = manager.ruleDicomTag;
            _rules = manager.rules;
            _sourceDicomFile = manager.sourceDicomFile;
            _destDicomFile = manager.destDicomFile;
            _dicomRequest = manager.dicomRequest;
            _stream = manager.stream;
        }

        public RoutedItem Clone(RoutedItem Item)
        {
            Throw.IfNull(Item);

            RoutedItemEx ri = new RoutedItemEx
            {
                args = Item.args,
                attempts = Item.attempts,
                AccessionNumber = Item.AccessionNumber,

                box = Item.box,

                creationTimeUtc = Item.creationTimeUtc,
                cloudTaskResults = Item.cloudTaskResults.ToList(),
                Compress = Item.Compress,

                destDicomFile = _destDicomFile,
                destFileName = Item.destFileName,
                destFileType = Item.destFileType,
                dicomRequest = _dicomRequest,

                Error = Item.Error,

                fileCount = Item.fileCount,
                fileIndex = Item.fileIndex,
                from = Item.from,
                fromConnection = Item.fromConnection,


                hl7 = Item.hl7.ToList(),

                id = Item.id,
                InstanceID = Item.InstanceID,

                lastAccessTimeUtc = Item.lastAccessTimeUtc,
                //ri.lastAttempt = lastAttempt;
                lastWriteTimeUtc = Item.lastWriteTimeUtc,

                length = Item.length,
                name = Item.name,

                matches = Item.matches,
                MessageId = Item.MessageId,

                PatientID = Item.PatientID,
                PatientIDIssuer = Item.PatientIDIssuer,
                priority = Item.priority,

                request = Item.request,
                requestType = Item.requestType,
                resource = Item.resource,
                response = Item.response.ToList(),
                resultsTime = Item.resultsTime,
                RoutedItemMetaFile = Item.RoutedItemMetaFile,
                ruleDicomTag = _ruleDicomTag,
                ruleDicomTags = _ruleDicomTags,
                rules = _rules,

                Series = Item.Series,
                Sop = Item.Sop,
                sourceDicomFile = _sourceDicomFile,
                sourceFileName = Item.sourceFileName,
                sourceFileType = Item.sourceFileType,
                status = Item.status,
                startTime = Item.startTime,
                Study = Item.Study,
                StudyID = Item.StudyID,
                stream = _stream,

                TagData = Item.TagData,
                TaskID = Item.TaskID,
                to = Item.to,
                toConnections = Item.toConnections,
                type = Item.type
            };

            return ri;
        }
    }
}
