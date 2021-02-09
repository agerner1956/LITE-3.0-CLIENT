using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Lite.Services
{
    public sealed class ProfileWriter : IProfileWriter
    {
        private readonly ILogger _logger;
        private readonly IFileProfileWriter _fileProfileWriter;
        private readonly ICloudProfileWriterService _cloudProfileWriter;
        private readonly IConnectionFinder _connectionFinder;

        public ProfileWriter(
            IFileProfileWriter fileProfileWriter,
            ICloudProfileWriterService cloudProfileWriter,
            IConnectionFinder connectionFinder,
            ILogger<ProfileWriter> logger)
        {
            _fileProfileWriter = fileProfileWriter;
            _cloudProfileWriter = cloudProfileWriter;
            _connectionFinder = connectionFinder;
            _logger = logger;

        }

        public async Task SaveProfile(Profile profile)
        {
            Throw.IfNull(profile);

            var taskInfo = "";

            try
            {
               await SyncWithCloud(profile);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"{taskInfo} Task Canceled");
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                _logger.Log(LogLevel.Critical, $"{taskInfo} HttpRequestException: {e.Message} {e.StackTrace}");

            }
            catch (NullReferenceException)
            {
                _logger.Log(LogLevel.Critical, $"{taskInfo} no putCloudProfile arg. Remote Monitoring Disabled!!");
            }
            catch (InvalidOperationException e)
            {
                _logger.Log(LogLevel.Warning, $"{taskInfo} unable to putCloudProfile due to concurrent Profile modification. {e.Message} {e.StackTrace}");
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Critical, $"{taskInfo} unable to putCloudProfile due to concurrent Profile modification. {e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                {
                    _logger.Log(LogLevel.Critical, $"Inner Exception: {e.InnerException}");
                }
            }

            try
            {
                if (profile.startupParams.saveProfilePath != null)
                {
                    _fileProfileWriter.Save(profile, profile.startupParams.saveProfilePath);

                    // During write set modified date to now so it doesn't go back around and reload
                    Profile.modifiedDate = DateTime.Now;
                }
            }
            catch (NullReferenceException)
            {
                _logger.Log(LogLevel.Critical, $"{taskInfo} no saveProfileFile arg. Agent startup may not include latest config!!  Set saveProfileFile to the same value as loadProfileFile.");
            }
        }

        private async Task SyncWithCloud(Profile profile)
        {
            var cloudConn = _connectionFinder.GetPrimaryLifeImageConnection(profile);
            if (profile.startupParams.putServerProfile && !cloudConn.loginNeeded)
            {
                await _cloudProfileWriter.PutConfigurationToCloud(profile, cloudConn);

                // During write set modified date to now so it doesn't go back around and reload
                Profile.modifiedDate = DateTime.Now;
            }
        }
    }
}
