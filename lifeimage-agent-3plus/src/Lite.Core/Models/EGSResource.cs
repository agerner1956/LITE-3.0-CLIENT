using System.Collections.Generic;

namespace Lite.Core.Models
{
    public class EGSResource
    {
        public string box;
        public string resource;
    }

    public class FilesModel
    {
        public List<RoutedItem> files = new List<RoutedItem>();

        public FilesModel()
        {

        }
        public FilesModel(List<RoutedItem> files)
        {
            this.files = files;
        }
    }
}
