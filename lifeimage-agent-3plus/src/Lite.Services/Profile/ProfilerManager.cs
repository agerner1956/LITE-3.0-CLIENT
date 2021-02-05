using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;

namespace Lite.Services
{
    public sealed class ProfileManager : IProfileManager
    {
        private readonly IUtil _util;
        private readonly IProfileLoaderService _profileLoader;
        private readonly IProfileMerger _profileMerger;
        private readonly ILogger _logger;

        public ProfileManager(
            IUtil util,
            IProfileLoaderService profileLoaderService,
            IProfileMerger profileMerger,
            ILogger<ProfileManager> logger)
        {
            _util = util;
            _profileLoader = profileLoaderService;
            _profileMerger = profileMerger;
            _logger = logger;
        }


        public void LoadProfile(Profile profile, string filename)
        {
            Throw.IfNullOrWhiteSpace(filename);

            var tmpPath = _util.GetTempFolder(filename);
            FileInfo oFileInfo = new FileInfo(tmpPath);

            var currentDir = Directory.GetCurrentDirectory();
            _logger.LogDebug($"Current dir: {currentDir}");

            if (oFileInfo.LastWriteTime > Profile.modifiedDate || Profile.modifiedDate == null || Profile._overrideVersionAndModifiedDate == true)
            {
                Profile newProfile = _profileLoader.Load(filename);

                // Spit out errors, some of which may be fatal
                foreach (string error in newProfile.errors)
                {
                    _logger.Log(LogLevel.Warning, error);
                }

                if (newProfile.IsInError() == false)
                {
                    //profile.MergeProfile(newProfile);
                    _profileMerger.MergeProfile(profile, newProfile);
                    //_logger.Log(TraceEventType.Verbose, $"loaded profile {filename} {JValue.Parse(profile.ToString()).ToString(Formatting.Indented)}");
                }
                else
                {
                    _logger.Log(LogLevel.Critical, $"Profile {profile.startupParams.localProfilePath} failed to load {profile.jsonInError}");

                    throw new Exception($"Bad Json File: {profile.jsonInError}");
                }

                Profile.modifiedDate = oFileInfo.LastWriteTime;
            }
        }
    }
}