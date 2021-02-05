using Dicom.Network;
using Lite.Core.Connections;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dicom.Features
{
    public interface IDicomCommand
    {
        void InitClient(DicomClient client);
    }
}
