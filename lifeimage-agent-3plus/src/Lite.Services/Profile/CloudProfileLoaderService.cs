using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Interfaces;
using Lite.Core.Json;
using Lite.Services.Connections;
using Lite.Services.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class CloudProfileLoaderService : ICloudProfileLoaderService
    {
        public const string AgentConfigurationUrl = "/api/agent/v1/agent-configuration";

        private readonly IProfileJsonHelper _jsonHelper;
        private readonly IProfileMerger _profileMerger;
        private readonly ILogger _logger;        
        private readonly ILiteHttpClient _liteHttpClient;

        public CloudProfileLoaderService(
            IProfileJsonHelper jsonHelper, 
            IProfileMerger profileMerger,            
            ILiteHttpClient liteHttpClient,
            ILogger<CloudProfileLoaderService> logger)
        {
            _profileMerger = profileMerger;            
            _jsonHelper = jsonHelper;
            _liteHttpClient = liteHttpClient;
            _logger = logger;
        }

        public async Task<Profile> GetAgentConfigurationFromCloud(LifeImageCloudConnection conn, string rowVersion, bool _overrideVersionAndModifiedDate)
        {
            var taskInfo = $"{conn.name}:";
            string json = "";

            var httpClient = _liteHttpClient.GetClient(conn);

            try
            {
                //set the URL
                string profileURL = conn.URL;
                if (rowVersion == null | _overrideVersionAndModifiedDate == true)
                {
                    profileURL += AgentConfigurationUrl;
                }
                else
                {
                    profileURL += $"{AgentConfigurationUrl}?version={rowVersion}";
                }

                _logger.Log(LogLevel.Debug, $"{taskInfo} getProfileURL: {profileURL}");

                var cookies = _liteHttpClient.GetCookies(profileURL);
                _logger.LogCookies(cookies, taskInfo);

                // issue the GET
                HttpResponseMessage httpResponse = await httpClient.GetAsync(profileURL);
                if (httpResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    return null;
                }

                string response = await httpResponse.Content.ReadAsStringAsync();

                _logger.Log(LogLevel.Debug, $"{taskInfo} response size: {response.Length}");

                if (httpResponse.StatusCode == HttpStatusCode.OK && response != null && response.Length > 0)
                {
                    // Cloud returns results in a map "configFile" -> value
                    Dictionary<string, string> map = JsonHelper.DeserializeFromMap(response);

                    map.TryGetValue("configFile", out string json64);

                    // Convert back from base 64 (needed because json was getting munged)
                    byte[] jsonBytes = Convert.FromBase64String(json64);
                    json = System.Text.Encoding.Default.GetString(jsonBytes);

                    _logger.Log(LogLevel.Debug, $"{taskInfo} Profile successfully downloaded from cloud.");
                    _logger.Log(LogLevel.Debug, $"{taskInfo} Raw JSON: \n {json}");

                    map.TryGetValue("version", out rowVersion);
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"{taskInfo} No profile update available from cloud.");
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
                _logger.LogFullException(e, $"{taskInfo} {e.Message}");
                _liteHttpClient.DumpHttpClientDetails();

                //throw e;  //solves a perpetual state of unauthorized, now solved by inspection of response code in other liCloud calls
            }

            if (json == "")
            {
                return null;
            }

            return _jsonHelper.DeserializeObject(json);
        }

        public async Task<Profile> LoadProfile(Profile source, LifeImageCloudConnection conn)
        {
            var taskInfo = "";
            Profile newProfile = null;

            try
            {
                if (source.startupParams.getServerProfile == true)
                {
                    newProfile = await GetAgentConfigurationFromCloud(conn, Profile.rowVersion, Profile._overrideVersionAndModifiedDate);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, $"{taskInfo} Problem getting server profile. Remote Configuration Not Possible!!");
                throw e;
            }

            _profileMerger.MergeProfile(source, newProfile);
            //source.MergeProfile(newProfile);

            return source;
        }
    }
}
