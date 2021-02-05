using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Json;
using Lite.Services.Connections;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;

namespace Lite.Services
{
    public sealed class CloudProfileWriterService : ICloudProfileWriterService
    {
        private readonly IProfileValidator _profileValidator;
        private readonly ILiteHttpClient _liteHttpClient;
        private readonly ILogger _logger;
 
        public CloudProfileWriterService(
            IProfileValidator profileValidator,
            ILiteHttpClient liteHttpClient,            
            ILogger<CloudProfileWriterService> logger)
        {
            _profileValidator = profileValidator;
            _liteHttpClient = liteHttpClient;
            _logger = logger;
        }

        public async Task PutConfigurationToCloud(Profile profile, LifeImageCloudConnection conn)
        {
            var taskInfo = $"PutProfile";

            if (conn == null)
            {
                //this can be called early in startup during replacement strategy
                await Task.CompletedTask;
                return;
            }

            var httpClient = _liteHttpClient.GetClient(conn);

            try
            {
                //set the URL
                string profileURL = conn.URL + $"/api/agent/v1/agent-configuration?version={Profile.rowVersion}";
                _logger.Log(LogLevel.Debug, $"{taskInfo} putProfileURL: {profileURL}");

                // validate and put any errors in the profile so it can be returned to the server
                profile.errors = _profileValidator.FullValidate(profile, profile.ToString());

                string json = profile.ToString();

                byte[] toBytes = Encoding.ASCII.GetBytes(json);
                string json64 = Convert.ToBase64String(toBytes);

                //            string json64 = Convert.ToBase64String(json);
                using HttpContent httpContent = new StringContent(json64);
                var cookies = _liteHttpClient.GetCookies(profileURL);
                _logger.LogCookies(cookies, taskInfo);

                HttpResponseMessage response = httpClient.PutAsync(profileURL, httpContent).Result;

                // output the result
                _logger.LogHttpResponseAndHeaders(response, taskInfo);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string resp = await response.Content.ReadAsStringAsync();

                    _logger.Log(LogLevel.Debug, $"{taskInfo} Profile successfully uploaded to cloud.");

                    // Read out server version in case null was passed in, essentially allow the agent to write once
                    // without a version (just in case its needed say to load a local version to the server) then the 
                    // version needs to be respected. In practice profile should be read first but that's not currently required.
                    Dictionary<string, string> map = JsonHelper.DeserializeFromMap(resp);

                    map.TryGetValue("version", out Profile.rowVersion);
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Profile version from server: {Profile.rowVersion}.");
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} {response.StatusCode}");

                    _liteHttpClient.DumpHttpClientDetails();
                }
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            catch (InvalidOperationException e)
            {
                if (e.InnerException != null && e.InnerException.Message != null)
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} Unable to upload profile to cloud because profile is being iterated.  Will try again: {e.Message} {e.StackTrace} Inner Exception: {e.InnerException.Message}");
                    if (e.InnerException.StackTrace != null)
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} e.InnerException.StackTrace: {e.InnerException.StackTrace}");
                    }
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"{taskInfo} Unable to upload profile to cloud because profile is being iterated.  Will try again: {e.Message} {e.StackTrace}");
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
            }
            catch (HttpRequestException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} Exception: {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Warning, $"Inner Exception: {e.InnerException}");
                }

                _liteHttpClient.DumpHttpClientDetails();
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);
                _liteHttpClient.DumpHttpClientDetails();
            }
        }
    }
}
