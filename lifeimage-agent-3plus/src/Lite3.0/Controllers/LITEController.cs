using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lite.Core;
using Lite.Core.Interfaces;
using Lite.Services.Connections;
using Lite3.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Lite3.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LITEController : ControllerBase
    {
        private readonly IConnectionFinder _connectionFinder;
        private readonly IProfileJsonHelper _profileJsonHelper;
        private readonly IConnectionManagerFactory _connectionManagerFactory; 
        private readonly ILogger _logger;

        public LITEController(            
            IConnectionFinder connectionFinder,
            IProfileJsonHelper profileJsonHelper,
            IConnectionManagerFactory connectionManagerFactory,
            ILogger<LITEController> logger)
        { 
            _connectionFinder = connectionFinder;
            _profileJsonHelper = profileJsonHelper;
            _connectionManagerFactory = connectionManagerFactory;
            _logger = logger;
        }

        [HttpPost]
        [DisableFormValueModelBinding]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, BufferBodyLengthLimit = long.MaxValue, MemoryBufferThreshold = int.MaxValue)]
        public async Task<IActionResult> Create()
        {
            try
            {               
                MemoryStream stream = new MemoryStream();
                await HttpContext.Request.Body.CopyToAsync(stream);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string profilejson = reader.ReadToEnd();
                Profile profile = _profileJsonHelper.DeserializeObject(profilejson);
                var connection = _connectionFinder.GetPrimaryLITEConnection(profile);
                var manager = _connectionManagerFactory.GetManager(connection) as ILiteConnectionManager;
                await manager.RegisterLITE(profile);
                return CreatedAtRoute("GetLITE", new { username = _connectionFinder.GetPrimaryLifeImageConnection(profile).username }, profile);
                //return CreatedAtRoute("GetFile", new { boxUuid = paths[0], fileID = paths[0] }, paths);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation($"Task was canceled.");
                return StatusCode(503);
            }
            catch (Exception e)
            {
                _logger.LogCritical($"{e.Message} {e.StackTrace}");
                return StatusCode(503);
            }
        }

        [HttpGet]
        public ActionResult<List<string>> GetAll()
        {
            List<string> list = new List<string>();
            foreach (var profile in LiteConnectionManager.LITERegistry)
            {
                list.Add(_connectionFinder.GetPrimaryLifeImageConnection(profile).username);
            }
            return list;
        }

        [HttpGet("{username}", Name = "GetLITE")]
        public ActionResult<Profile> GetLITE(string username)
        {
            var profiles = LiteConnectionManager.LITERegistry.ToList().FindAll(e => _connectionFinder.GetPrimaryLifeImageConnection(e).username == username);
            if (profiles.Count == 0)
            {
                return NotFound();
            }
            return profiles[0];
        }
    }
}
