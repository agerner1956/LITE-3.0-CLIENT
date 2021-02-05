using Dicom.Network;

namespace Lite.Services.Connections.Dicom.Features
{
    public abstract class DicomCommandBase : IDicomCommand
    {
        protected DicomClient dicomClient;

        public void InitClient(DicomClient client)
        {
            dicomClient = client;
        }
    }
}
