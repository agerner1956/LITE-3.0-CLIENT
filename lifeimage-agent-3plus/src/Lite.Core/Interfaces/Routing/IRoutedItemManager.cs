using Lite.Core.Connections;
using Lite.Core.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Lite.Core.Interfaces
{
    public interface IRoutedItemManager : System.IDisposable
    {
        void AgeAtExam();
        RoutedItem Clone();
        void Close();
        void Dequeue(Connection conn, BlockingCollection<RoutedItem> list, string queueName, bool error = false);
        void Dequeue(Connection conn, ObservableCollection<RoutedItem> list, string queueName, bool error = false);
        void DequeueCache(Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool error = false);
        void Enqueue(Connection conn, BlockingCollection<RoutedItem> list, string queueName, bool copy = false);
        void Enqueue(Connection conn, ObservableCollection<RoutedItem> list, string queueName, bool copy = false);
        void EnqueueCache(Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool copy = true);
        string GetSHA256(string source);
        //List<DicomTag> GetTagsInRules();
        void Init(RoutedItem item);
        void MoveFileToErrorFolder();        
        void Open();
        //void RemoveTagGroup(string tagGroup, string[] exceptions = null);
        //void RemoveUnspecifiedTags();
        void Translate(string tagString, string translationFileName, bool force);
    }
}
