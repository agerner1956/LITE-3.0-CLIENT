using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Models;
using Lite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Lite3.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivityController : ControllerBase
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILogger _logger;

        public ActivityController(
            IProfileStorage profileStorage,
            ILogger<ActivityController> logger)
        {
            _profileStorage = profileStorage;            
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<Dictionary<string, Dictionary<string, List<RoutedItem>>>>> GetActivity()
        {
            _logger.LogInformation("GetActivity() started");
            //Roll up RoutedItems by MetaData
            //ex: studyuid conn entertime exittime start last
            Dictionary<string, Dictionary<string, List<RoutedItem>>> connections = new Dictionary<string, Dictionary<string, List<RoutedItem>>>();
            Dictionary<string, List<RoutedItem>> queues = new Dictionary<string, List<RoutedItem>>();

            var profile = _profileStorage.Current;

            foreach (var conn in profile.connections)
            {
                queues.Add("toRules", conn.toRules.ToList());

                switch (conn.connType)
                {
                    case ConnectionType.dicom:
                        var dicom = (DICOMConnection)conn;
                        queues.Add("toDicom", dicom.toDicom.ToList());
                        break;
                    case ConnectionType.cloud:
                        var cloud = (LifeImageCloudConnection)conn;
                        queues.Add("toCloud", cloud.toCloud.ToList());
                        break;
                    case ConnectionType.file:
                        break;
                    case ConnectionType.hl7:
                        var hl7 = (HL7Connection)conn;
                        queues.Add("toHL7", hl7.ToHL7.ToList());
                        break;
                    case ConnectionType.dcmtk:
                        var dcmtk = (DcmtkConnection)conn;
                        queues.Add("toDcmsend", dcmtk.toDcmsend.ToList());
                        queues.Add("toDicom", dcmtk.toDicom.ToList());
                        queues.Add("toFindSCU", dcmtk.toFindSCU.ToList());
                        queues.Add("toMoveSCU", dcmtk.toMoveSCU.ToList());
                        break;
                    case ConnectionType.lite:
                        var lite = (LITEConnection)conn;
                        queues.Add("toEGS", lite.toEGS.ToList());
                        break;
                    case ConnectionType.other:
                    default:
                        break;
                }

                connections.Add(conn.name, queues);
            }

            return connections;
        }
    }
}
