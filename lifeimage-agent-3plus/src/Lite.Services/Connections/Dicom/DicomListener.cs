using Dicom;
using Dicom.Network;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dicom
{
    internal class DicomListener : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
    {
        public List<INetworkStream> clients = new List<INetworkStream>();

        public DicomTransferSyntax[] AcceptedTransferSyntaxes { get; set; } = new DicomTransferSyntax[]
        {
            DicomTransferSyntax.ImplicitVRLittleEndian,
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.ExplicitVRBigEndian
        };

        public DicomTransferSyntax[] AcceptedImageTransferSyntaxes { get; set; } = new DicomTransferSyntax[]
        {
            DicomTransferSyntax.ImplicitVRLittleEndian,
            DicomTransferSyntax.ExplicitVRLittleEndian,
            //DicomTransferSyntax.ImplicitVRBigEndian, invalid transfer syntax per https://github.com/fo-dicom/fo-dicom/issues/703
            DicomTransferSyntax.ExplicitVRBigEndian,

            DicomTransferSyntax.DeflatedExplicitVRLittleEndian,
            DicomTransferSyntax.GEPrivateImplicitVRBigEndian,
            DicomTransferSyntax.HEVCH265Main10ProfileLevel51,
            DicomTransferSyntax.HEVCH265MainProfileLevel51,

            DicomTransferSyntax.JPEGLSLossless,
            DicomTransferSyntax.JPEGLSNearLossless,

            DicomTransferSyntax.JPEGProcess14SV1,
            DicomTransferSyntax.JPEGProcess14,

            DicomTransferSyntax.JPEG2000Lossless,
            DicomTransferSyntax.JPEG2000Lossy,
            DicomTransferSyntax.JPEG2000Part2MultiComponent,
            DicomTransferSyntax.JPEG2000Part2MultiComponentLosslessOnly,

            DicomTransferSyntax.JPEGProcess1,
            DicomTransferSyntax.JPEGProcess2_4,
            DicomTransferSyntax.JPEGProcess10_12Retired,
            DicomTransferSyntax.JPEGProcess11_13Retired,

            DicomTransferSyntax.JPEGProcess14,
            DicomTransferSyntax.JPEGProcess14SV1,

            DicomTransferSyntax.JPEGProcess15Retired,
            DicomTransferSyntax.JPEGProcess16_18Retired,
            DicomTransferSyntax.JPEGProcess17_19Retired,
            DicomTransferSyntax.JPEGProcess20_22Retired,
            DicomTransferSyntax.JPEGProcess21_23Retired,
            DicomTransferSyntax.JPEGProcess24_26Retired,
            DicomTransferSyntax.JPEGProcess25_27Retired,
            DicomTransferSyntax.JPEGProcess28Retired,
            DicomTransferSyntax.JPEGProcess29Retired,
            DicomTransferSyntax.JPEGProcess3_5Retired,
            DicomTransferSyntax.JPEGProcess6_8Retired,
            DicomTransferSyntax.JPEGProcess7_9Retired,

            DicomTransferSyntax.JPIPReferenced,
            DicomTransferSyntax.JPIPReferencedDeflate,

            DicomTransferSyntax.MPEG2,
            DicomTransferSyntax.MPEG2MainProfileHighLevel,

            DicomTransferSyntax.MPEG4AVCH264BDCompatibleHighProfileLevel41,
            DicomTransferSyntax.MPEG4AVCH264HighProfileLevel41,
            DicomTransferSyntax.MPEG4AVCH264HighProfileLevel42For2DVideo,
            DicomTransferSyntax.MPEG4AVCH264HighProfileLevel42For3DVideo,
            DicomTransferSyntax.MPEG4AVCH264StereoHighProfileLevel42,

            DicomTransferSyntax.Papyrus3ImplicitVRLittleEndianRetired,
            DicomTransferSyntax.RFC2557MIMEEncapsulation,
            DicomTransferSyntax.RLELossless,
            DicomTransferSyntax.XMLEncoding
        };
        
        private readonly IConnectionFinder _connectionFinder;
        private readonly IProfileStorage _profileStorage;
        private readonly IRoutedItemManager _routedItemManager;
        private readonly IUtil _util;
        private readonly ILITETask _taskManager;
        private readonly ILogger logger;

        public DicomListener(
            INetworkStream stream, 
            Encoding fallbackEncoding,
            IConnectionFinder connectionFinder,
            IProfileStorage profileStorage,
            IUtil util,
            IRoutedItemManager routedItemManager,
            ILITETask taskManager,
            ILogger<DicomListener> logger)
            : base(stream, fallbackEncoding, null)
        {
            clients.Add(stream);

            this.logger = logger;
            _connectionFinder = connectionFinder;
            _profileStorage = profileStorage;
            _routedItemManager = routedItemManager;
            _taskManager = taskManager;
            _util = util;

            var syntax = new { };
        }

        public DicomCEchoResponse OnCEchoRequest(DicomCEchoRequest request)
        {
            logger.Log(LogLevel.Information, $"Association Request (echo) ");

            return new DicomCEchoResponse(request, DicomStatus.Success);
        }


        /// Responds to a cstore request, which kicks off a StreamToRules task
        public DicomCStoreResponse OnCStoreRequest(DicomCStoreRequest request)
        {
            var taskID = _taskManager.NewTaskID();

            var taskInfo =
                $"task: {taskID} messageID: {request.MessageID} connection: {((DICOMConnection)(base.UserState)).name}";

            try
            {
                var fromConnection = _connectionFinder.GetDicomConnectionToLocalAETitle(_profileStorage.Current, Association.CalledAE);

                if (fromConnection == null)
                {
                    //We have an inbound Association.CalledAE that we aren't configured for
                    logger.Log(LogLevel.Warning, $"{taskInfo} There is no connection defined where the LocalAETitle matches the Association.CalledAE: {Association.CalledAE}");
                    return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
                }

                logger.Log(LogLevel.Information, $"{taskInfo} CStoreRequest from {fromConnection.name}");

                var conn = ((DICOMConnection)(base.UserState));
                // var dir = profile.tempPath + Path.DirectorySeparatorChar + ((DICOMConnection)(base.UserState)).name + Path.DirectorySeparatorChar + "toRules";
                // Directory.CreateDirectory(dir);
                // var filename = dir + Path.DirectorySeparatorChar + System.Guid.NewGuid();
                // LifeImageLite.Logger.logger.Log(TraceEventType.Verbose, $"{taskInfo} Moving from {request.File.File.Name} to {filename}");
                // request.File.File.Move(dstFileName: filename);
                RoutedItem routedItem = new RoutedItem(
                    fromConnection: fromConnection.name,
                    sourceFileName: request.File.File.Name,
                    taskID: taskID)
                {
                    type = RoutedItem.Type.DICOM
                };

                request.Dataset.TryGetValue<string>(DicomTag.PatientID, 0, out routedItem.PatientID);
                request.Dataset.TryGetValue<string>(DicomTag.AccessionNumber, 0, out routedItem.AccessionNumber);
                request.Dataset.TryGetValue<string>(DicomTag.StudyInstanceUID, 0, out routedItem.Study);
                request.Dataset.TryGetValue<string>(DicomTag.StudyID, 0, out routedItem.StudyID);

                foreach (var item in request.Command)
                {
                    logger.Log(LogLevel.Debug, $"Command tag: {item.Tag} value: {item.ValueRepresentation}");
                }

                routedItem.id =
                    $"PID:{routedItem.PatientID}, AN:{routedItem.AccessionNumber}"; // , UID:{routedItem.Study}";

                //profile.rules.SendToRules(routedItem).Wait();
                routedItem.priority = _util.GetPriority((ushort)request.Priority);

                _routedItemManager.Init(routedItem);
                _routedItemManager.Enqueue(conn, conn.toRules, nameof(conn.toRules), copy: true);
                DicomStatus status = DicomStatus.Success;

                DicomCStoreResponse response = new DicomCStoreResponse(request, status)
                {
                    //Dataset = request.Dataset
                };
                response.Command.AddOrUpdate(DicomTag.AffectedSOPInstanceUID,
                    request.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0));
                response.Command.AddOrUpdate(DicomTag.AffectedSOPClassUID,
                    request.Dataset.GetValue<string>(DicomTag.SOPClassUID, 0));
                return response;
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Critical, $"{taskInfo} {e.Message} {e.StackTrace}");

                if (e.InnerException != null)
                {
                    logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                }

                request.Dataset.AddOrUpdate(DicomTag.AffectedSOPInstanceUID,
                    request.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0));
                return new DicomCStoreResponse(request,
                    new DicomStatus(DicomStatus.ProcessingFailure,
                        $"CStore Response Exception: {e.Message} {e.StackTrace}")); //out of resources not much flexibility here
            }
            finally
            {
            }
        }

        public void OnConnectionClosed(Exception e)
        {
            if (e != null && e.Message != null)
            {
                logger.Log(LogLevel.Critical, $"Connection Closed: {e.Message}");
            }
            else
            {
                logger.Log(LogLevel.Debug, $"Connection Closed.");
            }
        }

        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            var taskInfo = $"Association Aborted: connection: {((DICOMConnection)(base.UserState)).name}";

            logger.Log(LogLevel.Error, $"{taskInfo} {reason}");
        }

        public async Task OnReceiveAssociationReleaseRequestAsync()
        {
            try
            {
                logger.Log(LogLevel.Information, "Association Released");

                await SendAssociationReleaseResponseAsync();
                this.Dispose();
            }
            catch (TaskCanceledException)
            {
                logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Critical, $"{e.Message} {e.StackTrace}");
            }
        }


        public async Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            try
            {
                //var taskInfo = $"task: connection: {((DICOMConnection)(base.UserState)).name}";
                var fromConnection = _connectionFinder.GetDicomConnectionToLocalAETitle(_profileStorage.Current, association.CalledAE);

                if (fromConnection == null)
                {
                    //We have an inbound Association.CalledAE that we aren't configured for
                    logger.Log(LogLevel.Warning,
                        $"There is no connection defined where the LocalAETitle matches the Association.CalledAE: {Association.CalledAE}");
                    await SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser,
                        DicomRejectReason.CalledAENotRecognized);
                }

                foreach (var pc in association.PresentationContexts)
                {
                    if (pc.AbstractSyntax == DicomUID.Verification)
                    {
                        pc.AcceptTransferSyntaxes(AcceptedTransferSyntaxes);
                    }
                    else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                    {
                        pc.AcceptTransferSyntaxes(AcceptedImageTransferSyntaxes);
                    }
                }

                await SendAssociationAcceptAsync(association);
            }
            catch (TaskCanceledException)
            {
                logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Critical, $"{e.Message} {e.StackTrace}");
            }
        }

        public void OnCStoreRequestException(string tempFileName, Exception e)
        {
            var taskInfo = $"task: connection: {((DICOMConnection)(base.UserState)).name}";

            logger.Log(LogLevel.Critical, $"{taskInfo} {tempFileName} {e.Message} {e.StackTrace}");
        }

        Task IDicomServiceProvider.OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            //var taskInfo = $"task: connection: {((DICOMConnection)(base.UserState)).name}";
            var fromConnection = _connectionFinder.GetDicomConnectionToLocalAETitle(_profileStorage.Current, association.CalledAE);

            if (fromConnection == null)
            {
                //We have an inbound Association.CalledAE that we aren't configured for
                logger.Log(LogLevel.Warning,
                    $"There is no connection defined where the LocalAETitle matches the Association.CalledAE: {Association.CalledAE}");
                return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser,
                    DicomRejectReason.CalledAENotRecognized);
            }


            //load possible and accepted transfer syntaxes into profile if not populated
            bool add = false;
            if (((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs == null)
                ((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs = new List<string>();
            if (((DICOMConnection)(base.UserState)).PossibleImageTransferSyntaxUIDs == null)
                ((DICOMConnection)(base.UserState)).PossibleImageTransferSyntaxUIDs = new List<string>();

            if (((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs.Count == 0) add = true;

            ((DICOMConnection)(base.UserState)).PossibleImageTransferSyntaxUIDs.Clear();

            foreach (var dicomTransferSyntax in AcceptedImageTransferSyntaxes)
            {
                logger.Log(LogLevel.Information,
                    $"Possible AcceptedImageTransferSyntaxes UID:{dicomTransferSyntax.UID} Endian:{dicomTransferSyntax.Endian} IsDeflate:{dicomTransferSyntax.IsDeflate} IsEncapsulated:{dicomTransferSyntax.IsEncapsulated} IsExplicitVR:{dicomTransferSyntax.IsExplicitVR} IsLossy:{dicomTransferSyntax.IsLossy} IsRetired:{dicomTransferSyntax.IsRetired} LossyCompressionMethod:{dicomTransferSyntax.LossyCompressionMethod} SwapPixelData:{dicomTransferSyntax.SwapPixelData}");

                if (!((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs.Exists(e =>
                   e.Equals(dicomTransferSyntax.UID.ToString())))
                {
                    if (add == true)
                    {
                        ((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs.Add(dicomTransferSyntax.UID
                            .ToString());
                    }
                    else
                    {
                        logger.Log(LogLevel.Warning,
                            $"Not configured in DICOMConnection.AcceptedImageTransferSyntaxUIDs!!  UID:{dicomTransferSyntax.UID}");
                    }
                }

                ((DICOMConnection)(base.UserState)).PossibleImageTransferSyntaxUIDs.Add(dicomTransferSyntax.UID
                    .ToString());
            }

            //load AcceptedImageTransferSyntaxes from profile now we are populated one way or another
            var newAcceptedImageTransferSyntaxes = new List<DicomTransferSyntax>();
            string uidnumber = "";
            foreach (var item in ((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs)
            {
                try
                {
                    uidnumber = item.Substring(item.LastIndexOf("[") + 1).TrimEnd(']'); //,item.IndexOf("]")-1
                    var transfersyntax = DicomTransferSyntax.Parse(uidnumber);
                    newAcceptedImageTransferSyntaxes.Add(transfersyntax);
                }
                catch (Exception)
                {
                    logger.Log(LogLevel.Critical, $"Unable to parse DICOMConnection.AcceptedImageTransferSyntaxUID: {uidnumber}");
                }
            }

            AcceptedImageTransferSyntaxes = newAcceptedImageTransferSyntaxes.ToArray();

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification)
                {
                    pc.AcceptTransferSyntaxes(AcceptedTransferSyntaxes);
                }
                else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                {
                    if (((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs == null ||
                        ((DICOMConnection)(base.UserState)).AcceptedImageTransferSyntaxUIDs.Count == 0)
                    {
                    }

                    pc.AcceptTransferSyntaxes(AcceptedImageTransferSyntaxes);
                }
            }

            return SendAssociationAcceptAsync(association);
        }

        Task IDicomServiceProvider.OnReceiveAssociationReleaseRequestAsync()
        {
            logger.Log(LogLevel.Information, "Association Released");

            return SendAssociationReleaseResponseAsync();
        }

        void IDicomService.OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            var taskInfo = $"Association Aborted: connection: {((DICOMConnection)(base.UserState)).name}";

            logger.Log(LogLevel.Error, $"{taskInfo} {reason}");
        }

        void IDicomService.OnConnectionClosed(Exception exception)
        {
            if (exception != null && exception.Message != null)
            {
                logger.Log(LogLevel.Critical, $"Connection Closed: {exception.Message}");
            }
            else
            {
                logger.Log(LogLevel.Debug, $"Connection Closed.");
            }

            foreach (var client in clients)
            {
                var stream = client.AsStream();
                stream.Flush();
                stream.Close();
                stream.Dispose();
            }
        }
    }
}
