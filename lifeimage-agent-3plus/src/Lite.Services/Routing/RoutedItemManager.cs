using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Routing.RouteItemManager;
using Lite.Services.Security;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Lite.Services
{
    using Dicom;
    using Dicom.Network;

    public class RoutedItemManager : RouteItemHandler, IRoutedItemManager
    {
        public List<Tag> ruleDicomTags;
        /// <summary>
        /// ruleDicomTag is populated to provide context to a tag script during execution.  
        /// The tag script will use this  property to know which tag on which it should operate.
        /// </summary>
        public Tag ruleDicomTag;
        public Rules rules;
        public DicomFile sourceDicomFile;
        public DicomFile destDicomFile;
        public DicomRequest dicomRequest;

        public Stream stream;

        private readonly IProfileStorage _profileStorage;        
        private readonly IDiskUtils _util;
        private readonly IEnqueueCacheService _enqueueCacheService;
        private readonly IEnqueueService _enqueueService;
        private readonly IEnqueueBlockingCollectionService _enqueueBlockingCollectionService;
        private readonly IDequeueService _dequeueService;
        private readonly IDequeueBlockingCollectionService _dequeueBlockingCollectionService;
        private readonly IDequeueCacheService _dequeueCacheService;
        private readonly ITranslateService _translateService;
        private readonly IAgeAtExamService _ageAtExamService;

        private bool _disposed;

        public RoutedItemManager(
            IProfileStorage profileStorage,
            IDiskUtils util,
            ITranslateService translateService,
            IAgeAtExamService ageAtExamService,
            IEnqueueCacheService enqueueCacheService,
            IEnqueueService enqueueService,
            IEnqueueBlockingCollectionService enqueueBlockingCollectionService,
            IDequeueService dequeueService,
            IDequeueBlockingCollectionService dequeueBlockingCollectionService,
            IDequeueCacheService dequeueCacheService,
            ILogger<RoutedItemManager> logger) : base(logger)
        {
            _util = util;
            _profileStorage = profileStorage;
            _enqueueCacheService = enqueueCacheService;
            _enqueueService = enqueueService;
            _enqueueBlockingCollectionService = enqueueBlockingCollectionService;
            _dequeueService = dequeueService;
            _dequeueBlockingCollectionService = dequeueBlockingCollectionService;
            _dequeueCacheService = dequeueCacheService;
            _translateService = translateService;
            _ageAtExamService = ageAtExamService;
        }

        public void Init(RoutedItem item)
        {
            Throw.IfNull(item);
            Item = item;
        }

        public RoutedItem Item { get; private set; }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        public void Open()
        {
            var taskInfo = "task: {ri.taskID}";
            try
            {
                if (Item.sourceFileName != null)
                {
                    if (sourceDicomFile == null && !Item.sourceFileName.EndsWith(".hl7"))
                    {
                        sourceDicomFile = DicomFile.Open(Item.sourceFileName);
                    }

                    if (stream == null && sourceDicomFile != null)
                    {
                        //stream = sourceDicomFile.File.Open();
                    }
                }
                else
                {
                    throw new Exception("fileName cannot be null");
                }
            }
            catch (Exception e)
            {
                WriteDetailedLog(e, Item, taskInfo);

                stream?.Close();
                sourceDicomFile = null;
                //throw e;
                throw;
            }
        }

        public void Close()
        {
            stream?.Close();
            stream = null;
            sourceDicomFile = null;
        }

        /// <summary>
        /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="copy"></param>
        public void EnqueueCache(Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool copy = true)
        {
            var taskInfo = $"task: {Item.TaskID} connection: {conn.name}";           

            try
            {
                _enqueueCacheService.EnqueueCache(Item, conn, list, queueName, copy);
            }
            catch (Exception e)
            {
                WriteDetailedLog(e, Item, taskInfo);
            }
        }

        /// <summary>
        /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="copy"></param>
        public void Enqueue(Connection conn, ObservableCollection<RoutedItem> list, string queueName, bool copy = false)
        {            
            _enqueueService.Enqueue(Item, conn, list, queueName, copy);
        }

        /// <summary>
        /// Enqueue serializes and adds a RoutedItem metadata and related file artifact(s) to a specified connection and list
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="copy"></param>
        public void Enqueue(Connection conn, BlockingCollection<RoutedItem> list, string queueName, bool copy = false)
        {
            _enqueueBlockingCollectionService.Enqueue(Item, conn, list, queueName, copy);
        }

        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="error"></param>
        public void DequeueCache(Connection conn, Dictionary<string, List<RoutedItem>> list, string queueName, bool error = false)
        {
            _dequeueCacheService.DequeueCache(Item, conn, list, queueName, error);
        }

        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="error"></param>
        public void Dequeue(Connection conn, ObservableCollection<RoutedItem> list, string queueName, bool error = false)
        {
            _dequeueService.Dequeue(Item, conn, queueName, error, stream);
        }

        /// <summary>
        /// Dequeue removes RoutedItem metadata and related file artifact(s) from disk and from a specified connection and list
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="list"></param>
        /// <param name="queueName"></param>
        /// <param name="error"></param>
        public void Dequeue(Connection conn, BlockingCollection<RoutedItem> list, string queueName, bool error = false)
        {
            _dequeueBlockingCollectionService.Dequeue(Item, conn, list, queueName, stream, error);
        }

        public RoutedItem Clone()
        {
            RoutedItemClonable clonable = new RoutedItemClonable(this);
            return clonable.Clone(Item);
        }

        /// <summary>
        /// Convenience method for AgeAtExam script to calculate the age at exam and update the specified tag in the Rule.
        /// </summary>
        public void AgeAtExam()
        {
            var taskInfo = $"task: {Item.TaskID} fromConnection: {Item.fromConnection}";

            try
            {
                var @params = new AgeAtExamServiceParams(sourceDicomFile, ruleDicomTag);
                _ageAtExamService.AgeAtExam(@params, taskInfo);
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
        }

        /// <summary>
        /// Convenience method for comparison-ready list for use in scripts such as WhiteList.
        /// </summary>
        /// <returns></returns>
        public List<DicomTag> GetTagsInRules()
        {
            List<DicomTag> tags = new List<DicomTag>();
            try
            {
                foreach (var destrule in rules.destRules)
                {
                    foreach (var ruleDicomTag in destrule.ruleTags)
                    {
                        DicomTag dtTag = DicomTag.Parse(ruleDicomTag.tag);
                        tags.Add(dtTag);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
            }
            return tags;
        }

        /// <summary>
        /// Convenience method for WhiteList script to remove tags not specified in rules.
        /// </summary>
        public void RemoveUnspecifiedTags()
        {
            var taskInfo = $"task: {Item.TaskID} fromConnection: {Item.fromConnection}";

            _logger.Log(LogLevel.Information, $"{taskInfo} found {sourceDicomFile.Dataset.Count()} tags in DICOM.");
            if (sourceDicomFile.Dataset.Any())
            {
                foreach (var tag in sourceDicomFile.Dataset)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo}  tag: {tag}");
                }
            }

            var tagsInRules = GetTagsInRules();
            _logger.Log(LogLevel.Information, $"{taskInfo} found {tagsInRules.Count} tags specified in rules.");

            var tagsToRemove = new List<DicomTag>();
            foreach (var tag in sourceDicomFile.Dataset)
            {
                if (!tagsInRules.Exists(e => e.Equals(tag.Tag)))
                {
                    tagsToRemove.Add(tag.Tag);
                }
            }
            foreach (var tagToRemove in tagsToRemove)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} removing unspecified tag: {tagToRemove}.");
                sourceDicomFile.Dataset.Remove(tagToRemove);
            }

            _logger.Log(LogLevel.Information, $"{taskInfo} {sourceDicomFile.Dataset.Count()} tags remain in DICOM.");

            if (sourceDicomFile.Dataset.Count() > 0)
            {
                foreach (var remainingTag in sourceDicomFile.Dataset)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} remaining tag: {remainingTag}");
                }
            }
        }

        /// <summary>
        /// Convenience method for RemoveGroupxxxs scripts to remove tags belonging to a group.
        /// </summary>
        /// <param name="tagGroup"></param>
        /// <param name="exceptions"></param>
        public void RemoveTagGroup(string tagGroup, string[] exceptions = null)
        {
            var taskInfo = $"task: {Item.TaskID} fromConnection: {Item.fromConnection} tagGroup: {tagGroup}";
            List<string> exceptionList = null;
            if (exceptions != null)
            {
                exceptionList = new List<string>(exceptions);
                foreach (var exception in exceptionList)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo}  exception: {exception}");
                }
            }

            ushort ushortTagGroup = 0;
            if (!string.IsNullOrEmpty(tagGroup))
            {
                ushortTagGroup = Convert.ToUInt16(tagGroup, 16);
            }

            _logger.Log(LogLevel.Information, $"{taskInfo} found {sourceDicomFile.Dataset.Count()} tags in DICOM.");
            if (sourceDicomFile.Dataset.Any())
            {
                foreach (var tag in sourceDicomFile.Dataset)
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo}  tag: {tag}");
                }
            }

            // var tagsInRules = GetTagsInRules();
            // _logger.Log(LogLevel.Information, $"{taskInfo} found {tagsInRules.Count} tags specified in rules.");

            var tagsToRemove = new List<DicomTag>();
            foreach (var tag in sourceDicomFile.Dataset)
            {
                if (tag.Tag.Group == ushortTagGroup)
                {
                    var tagString = tag.Tag.ToString("g", null);
                    if (exceptionList != null)
                    {
                        if (exceptionList.Exists(e => e == tagString))
                        {
                            _logger.Log(LogLevel.Information, $"{taskInfo} Not removing {tagString} because exception is defined for this tag.");
                        }
                        else
                        {
                            tagsToRemove.Add(tag.Tag);
                        }
                    }
                    else
                    {
                        tagsToRemove.Add(tag.Tag);
                    }
                }
            }
            foreach (var tagToRemove in tagsToRemove)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} removing tag: {tagToRemove} in group: {tagGroup} {ushortTagGroup}.");
                sourceDicomFile.Dataset.Remove(tagToRemove);
            }

            _logger.Log(LogLevel.Information, $"{taskInfo} {sourceDicomFile.Dataset.Count()} tags remain in DICOM.");

            if (sourceDicomFile.Dataset.Any())
            {
                foreach (var remainingTag in sourceDicomFile.Dataset)
                {
                    _logger.Log(LogLevel.Information, $"{taskInfo} remaining tag: {remainingTag}");
                }
            }
        }

        /// <summary>
        /// Look up translation for specified tag, write translationhistory record and replace tag contents.
        /// </summary>
        /// <param name="tagString"></param>
        /// <param name="translationFileName"></param>
        /// <param name="force"></param>
        public void Translate(string tagString, string translationFileName, bool force)
        {
            var @params = new TranslateServiceParams(sourceDicomFile, tagString, translationFileName, force);
            _translateService.Translate(Item, @params);
        }

        public string GetSHA256(string source)
        {
            var taskInfo = $"task: {Item.TaskID} fromConnection: {Item.fromConnection} source: {source}";

            using SHA256 sha256Hash = SHA256.Create();
            HashHelper hashHelper = new HashHelper(sha256Hash);

            string hash = hashHelper.GetHash(source);

            _logger.Log(LogLevel.Debug, $"{taskInfo} hash: {hash}");

            if (hashHelper.VerifyHash(source, hash))
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Verified: The hashes are the same.");
            }
            else
            {
                _logger.Log(LogLevel.Critical, $"{taskInfo} The hashes are NOT the same.");
            }

            return hash;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                stream?.Close();
                stream = null;
                sourceDicomFile = null;
                destDicomFile = null;
            }

            _disposed = true;
        }

        public void MoveFileToErrorFolder()
        {
            _util.MoveFileToErrorFolder(_profileStorage.Current.tempPath, Item.sourceFileName);
        }
    }
}
