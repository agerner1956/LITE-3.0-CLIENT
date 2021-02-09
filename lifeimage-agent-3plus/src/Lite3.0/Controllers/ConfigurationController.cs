using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Services;
using Lite3.Hubs;
using Lite3.Infrastructure.Helpers;
using Lite3.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Lite3.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigurationController : ControllerBase
    {
        private readonly IProfileStorage _profileStorage;
        private readonly IConnectionFinder _connectionFinder;
        private readonly ILogger _logger;
        private readonly IHubContext<ChatHub> _hubContext;

        public ConfigurationController(
            IProfileStorage profileStorage,
            IConnectionFinder connectionFinder,
            ILogger<ConfigurationController> logger, 
            IHubContext<ChatHub> hubContext)
        {
            _profileStorage = profileStorage;
            _connectionFinder = connectionFinder;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<ActionResult<ConnectionSet>> GetShares()
        {
            ConnectionSet connectionSet = new ConnectionSet();

            try
            {
                var profile = _profileStorage.Current;
                var connection = _connectionFinder.GetPrimaryLITEConnection(profile);
                var dirs = Directory.GetDirectories(connection.resourcePath + Path.DirectorySeparatorChar + connection.name);

                foreach (var dir in dirs)
                {
                    ShareDestinations dest = new ShareDestinations
                    {
                        boxUuid = dir.Replace(profile.tempPath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar, "")
                    };
                    connectionSet.shareDestinations.Add(dest);
                    connectionSet.connectionName = connection.ServicePoint;
                }
            }
            catch (System.IO.DirectoryNotFoundException)
            {
            }

            return connectionSet;
        }

        [HttpGet("{box}", Name = "GetConfigurations")]
        public async Task<ActionResult<FilesModel>> GetFiles(string box)
        {
            FilesModel files = new FilesModel();

            try
            {
                var profile = _profileStorage.Current;
                var connection = _connectionFinder.GetPrimaryLITEConnection(profile);
                var path = connection.resourcePath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + box;
                Directory.CreateDirectory(path);
                var temp = Directory.GetFiles(path);

                foreach (var file in temp)
                {
                    if (file != ".DS_Store" && !file.EndsWith(".tmp"))
                    {
                        try
                        {
                            var info = new FileInfo(file);
                            RoutedItem ri = new RoutedItem
                            {
                                type = RoutedItem.Type.FILE,
                                name = file.Replace(connection.resourcePath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + box + Path.DirectorySeparatorChar, ""),
                                resource = file.Replace(connection.resourcePath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + box + Path.DirectorySeparatorChar, ""),
                                box = box,
                                creationTimeUtc = info.CreationTimeUtc,
                                lastWriteTimeUtc = info.LastWriteTimeUtc,
                                lastAccessTimeUtc = info.LastAccessTimeUtc,
                                length = info.Length
                            };

                            files.files.Add(ri);
                            if (files.files.Count >= 1000)
                            {  //limit rows returned
                                break;
                            }
                        }
                        catch (FileNotFoundException) { }
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
            }

            return files;
        }

        [HttpGet("{boxUuid}/{fileID}", Name = "GetConfiguration")]
        public async Task<ActionResult<Stream>> GetFile(string boxUuid, string fileID)
        {
            try
            {
                var profile = _profileStorage.Current;
                var connection = _connectionFinder.GetPrimaryLITEConnection(profile);
                Response.ContentType = $"multipart/mixed; boundary={fileID.Substring(fileID.LastIndexOf("-b-") + 3)}";
                Response.Headers.Add("Content-Encoding", "gzip");
                var path = connection.resourcePath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + boxUuid + Path.DirectorySeparatorChar + fileID;
                return new FileInfo(path).OpenRead();
            }
            catch (FileNotFoundException)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }
            catch (IOException e)
            {
                if (e.Message.Contains("in use"))
                {
                    return StatusCode((int)HttpStatusCode.Locked);
                }
                else
                {
                    return StatusCode((int)HttpStatusCode.InternalServerError);
                }
            }
        }

        [HttpPost]
        [DisableFormValueModelBinding]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, BufferBodyLengthLimit = long.MaxValue, MemoryBufferThreshold = int.MaxValue)]
        public async Task<IActionResult> Create()
        {
            try
            {
                var profile = _profileStorage.Current;
                var shareDestinations = Request.Headers["X-Li-Destination"];

                List<string[]> paths = new List<string[]>();

                if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                {
                    _logger.LogCritical($"Expected a multipart request, but got {Request.ContentType}");
                    return BadRequest($"Expected a multipart request, but got {Request.ContentType}");
                }

                var boundary = MultipartRequestHelper.GetBoundary(
                    MediaTypeHeaderValue.Parse(Request.ContentType),
                    int.MaxValue);

                bool firstShare = true;
                string firstFile = null;

                var connection = _connectionFinder.GetPrimaryLITEConnection(profile);

                foreach (var shareDestination in shareDestinations)
                {
                    foreach (string share in shareDestination.Split(",", StringSplitOptions.RemoveEmptyEntries))
                    {
                        var filename = Guid.NewGuid() + "-b-" + boundary;
                        var targetDir = connection.resourcePath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + share.Trim();
                        var targetFile = connection.resourcePath + Path.DirectorySeparatorChar + connection.name + Path.DirectorySeparatorChar + share.Trim() + Path.DirectorySeparatorChar + filename;
                        string[] targetURI = { share.Trim(), filename.ToString() };

                        Directory.CreateDirectory(targetDir);

                        if (firstShare)
                        {
                            using (var targetStream = System.IO.File.Create(targetFile + ".tmp"))
                            {
                                await HttpContext.Request.Body.CopyToAsync(targetStream);
                                firstShare = false;
                                firstFile = targetFile;
                            }
                            System.IO.File.Move(targetFile + ".tmp", targetFile);
                        }
                        else
                        {
                            System.IO.File.Copy(firstFile, targetFile + ".tmp", true);
                            System.IO.File.Move(targetFile + ".tmp", targetFile);
                        }

                        paths.Add(targetURI);
                        _logger.LogInformation($"Copied the uploaded file '{targetFile}' uri '{targetURI}'");
                    }
                }

                if (paths.Count > 0)
                {
                    //notify all connected LITEs and EGS instances.  This needs to be more specific to those interested in sharing dests
                    await _hubContext.Clients.All.SendAsync("ReceiveMessage", User.Identity.Name, JsonSerializer.Serialize(paths));

                    //look up notify table to find clients that are interested in these sharing dests
                    await _hubContext.Clients.Groups(new List<string>(shareDestinations[0].Split(",", StringSplitOptions.RemoveEmptyEntries))).SendAsync("ReceiveMessage", User.Identity.Name, JsonSerializer.Serialize(paths));

                    //{boxUuid}/{fileID}
                    return CreatedAtRoute("GetFile", new { boxUuid = paths[0], fileID = paths[0] }, paths);
                }
                else
                {
                    _logger.LogCritical($"{(int)HttpStatusCode.UnprocessableEntity}");
                    return StatusCode((int)HttpStatusCode.UnprocessableEntity); //if there is no share dest
                }
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
    }
}
