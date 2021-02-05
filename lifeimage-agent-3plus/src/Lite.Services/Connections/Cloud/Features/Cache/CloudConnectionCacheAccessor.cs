using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Lite.Services.Connections.Cloud.Features
{
    public interface ICloudConnectionCacheAccessor
    {
        string GetCachedItemMetaData(LifeImageCloudConnection Connection, RoutedItem routedItem, long taskID);
    }

    public sealed class CloudConnectionCacheAccessor : ICloudConnectionCacheAccessor
    {
        private readonly ILogger _logger;

        public CloudConnectionCacheAccessor(ILogger<CloudConnectionCacheAccessor> logger)
        {
            _logger = logger;
        }

        public string GetCachedItemMetaData(LifeImageCloudConnection Connection, RoutedItem routedItem, long taskID)
        {
            Throw.IfNull(Connection);

            var taskInfo = $"conn: {Connection.name} taskID: {taskID}";
            RootObject rootObject = new RootObject
            {
                ImagingStudy = new List<ImagingStudy>()
            };

            ImagingStudy study = new ImagingStudy
            {
                accession = new Accession(),
                patient = new Patient(),
                series = new List<Series>()
            };

            var cacheItem = LifeImageCloudConnection.cache[routedItem.id].ToList();

            var query = cacheItem
                .GroupBy(item => new { item.Study, item.Series, item.PatientID, item.AccessionNumber })
                .Select(grp => new
                {
                    Study = grp.Key.Study,
                    Series = grp.Key.Series,
                    PatientID = grp.Key.PatientID,
                    Accession = grp.Key.AccessionNumber,
                    Instances = grp.Count()
                });

            foreach (var result in query)
            {
                _logger.Log(LogLevel.Debug, $"{taskInfo} Study:{result.Study} Series:{result.Series} PatientID:{result.PatientID} Accession:{result.Accession} Instances:{result.Instances}");
                var series = new Series
                {
                    number = result.Instances,
                    uid = result.Series
                };
                study.series.Add(series);
            }

            study.numberOfInstances = query.Sum(x => x.Instances);
            study.numberOfSeries = query.Count();
            rootObject.ImagingStudy.Add(study);

            var json = JsonSerializer.Serialize(rootObject);
            _logger.Log(LogLevel.Debug, $"{taskInfo} Json:{json}");

            return json;
        }
    }
}
