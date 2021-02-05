using Lite.Core.Models;

namespace Lite.Core.Utils
{
    public interface IDicomUtil
    {
        bool IsDICOM(RoutedItem routedItem);
        RoutedItem Dicomize(RoutedItem routedItem);
    }
}
