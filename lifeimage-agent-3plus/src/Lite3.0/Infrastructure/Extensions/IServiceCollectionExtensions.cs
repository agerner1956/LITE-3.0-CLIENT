using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Lite.Core.Interfaces.Scripting;
using Lite.Core.IoC;
using Lite.Core.Security;
using Lite.Core.Utils;
using Lite.Services;
using Lite.Services.Cache;
using Lite.Services.Config;
using Lite.Services.Configuration;
using Lite.Services.Connections;
using Lite.Services.Connections.Cloud;
using Lite.Services.Connections.Cloud.Features;
using Lite.Services.Connections.Dcmtk;
using Lite.Services.Connections.Dcmtk.Features;
using Lite.Services.Connections.Dicom;
using Lite.Services.Connections.Dicom.Features;
using Lite.Services.Connections.Files;
using Lite.Services.Connections.Files.Features;
using Lite.Services.Connections.Hl7;
using Lite.Services.Connections.Hl7.Features;
using Lite.Services.Connections.Lite.Features;
using Lite.Services.Http;
using Lite.Services.Routing.RouteItemManager;
using Lite.Services.Routing.RulesManagerFeatures;
using Lite.Services.Scripting;
using Lite.Services.Studies;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Lite3.Infrastructure.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceProvider AddLite(this IServiceCollection services)
        {
            Throw.IfNull(services);         

            // utils
            services.AddSingleton<IUtil, Util>();
            services.AddSingleton<IDiskUtils, DiskUtils>();
            services.AddSingleton<IDicomUtil, DicomUtil>();
            services.AddSingleton<IDcmtkUtil, DcmtkUtil>();
            services.AddSingleton<ICrypto, Crypto>();

            // common
            services.AddSingleton<ILoggerManager, LoggerManager>();
            services.AddSingleton<IScriptService, ScriptService>();
            services.AddSingleton<IProfileStorage, ProfileStorage>();
            services.AddSingleton<ILiteConfigService, LiteConfigService>();
            services.AddSingleton<IConnectionFinder, ConnectionFinder>();
            services.AddSingleton<ILitePurgeService, LitePurgeService>();
            services.AddSingleton<IX509CertificateService, X509CertificateService>();
            services.AddSingleton<IRoutedItemLoader, RoutedItemLoader>();
            services.AddTransient<ILiteHttpClient, LiteHttpClient>();
            services.AddTransient<IConfigurationLoader, ConfigurationLoader>();

            services.AddTransient<IDuplicatesDetectionService, DuplicatesDetectionService>();

            // routing features
            services.AddTransient<IEnqueueCacheService, EnqueueCacheService>();
            services.AddTransient<IEnqueueService, EnqueueService>();
            services.AddTransient<IEnqueueBlockingCollectionService, EnqueueBlockingCollectionService>();            
            services.AddTransient<IDequeueService, DequeueService>();
            services.AddTransient<IDequeueBlockingCollectionService, DequeueBlockingCollectionService>();            
            services.AddTransient<IDequeueCacheService, DequeueCacheService>();            
            services.AddTransient<ITranslateService, TranslateService>();
            services.AddTransient<IAgeAtExamService, AgeAtExamService>();            
            services.AddTransient<IRoutedItemManager, RoutedItemManager>();

            // rules manager
            services.AddTransient<IRulesEvalService, RulesEvalService>();
            services.AddTransient<IRunPreProcessToConnectionScriptsService, RunPreProcessToConnectionScriptsService>();
            services.AddTransient<IRunPreProcessFromConnectionScriptsService, RunPreProcessFromConnectionScriptsService>();
            services.AddTransient<IRunPostProcessFromConnectionScriptsService, RunPostProcessFromConnectionScriptsService>();
            services.AddTransient<IRunPostProcessToConnectionScriptsService, RunPostProcessToConnectionScriptsService>();            
            services.AddTransient<IDoTagsMatchService, DoTagsMatchService>();
            services.AddTransient<IDoesRuleMatchService, DoesRuleMatchService>();
            services.AddTransient<ICheckAndDelayOnWaitConditionsService, CheckAndDelayOnWaitConditionsService>();
            services.AddTransient<IRulesManager, RulesManager>();

            // profile features
            services.AddTransient<IProfileConnectionsInitializer, ProfileConnectionsInitializer>();
            services.AddTransient<ICloudProfileLoaderService, CloudProfileLoaderService>();
            services.AddTransient<ICloudProfileWriterService, CloudProfileWriterService>();
            services.AddTransient<IProfileJsonHelper, ProfileJsonHelper>();
            services.AddTransient<IProfileLoaderService, ProfileLoaderService>();
            services.AddTransient<IProfileValidator, ProfileValidator>();
            services.AddTransient<IProfileWriter, ProfileWriter>();
            services.AddTransient<IFileProfileWriter, FileProfileWriter>();
            services.AddTransient<IProfileManager, ProfileManager>();
            services.AddTransient<IProfileMerger, ProfileMerger>();

            // studies
            services.AddTransient<IStudyManager, StudyManager>();
            services.AddTransient<IStudiesDownloadManager, StudiesDownloadManager>();

            // connection manager factory
            services.AddTransient<IConnectionManagerFactory, ConnectionManagerFactory>();

            // cache
            services.AddTransient<IConnectionCacheResponseService, ConnectionCacheResponseService>();
            services.AddTransient<IConnectionToRulesManagerAdapter, ConnectionToRulesManagerAdapter>();

            // connection managers           
            services.AddTransient<ILifeImageCloudConnectionManager, LifeImageCloudConnectionManager>();
            services.AddTransient<IDicomConnectionManager, DicomConnectionManager>();
            services.AddTransient<IHl7ConnectionManager, Hl7ConnectionManager>();
            services.AddTransient<IFileConnectionManager, FileConnectionManager>();
            services.AddTransient<IDcmtkConnectionManager, DcmtkConnectionManager>();
            services.AddTransient<ILiteConnectionManager, LiteConnectionManager>();

            // cloud features
            services.AddTransient<ICloudPingService, CloudPingService>();
            services.AddTransient<ICloudKeepAliveService, CloudKeepAliveService>();            
            services.AddTransient<ICloudDownloadService, CloudDownloadService>();
            services.AddTransient<ICloudUploadService, CloudUploadService>();
            services.AddTransient<ICloudShareDestinationsService, CloudShareDestinationsService>();
            services.AddTransient<ICloudLoginService, CloudAuthenticationService>();
            services.AddTransient<ICloudRegisterService, CloudRegisterService>();
            services.AddTransient<ICloudLogoutService, CloudAuthenticationService>();
            services.AddTransient<IStowAsMultiPartCloudService, StowAsMultiPartCloudService>();
            services.AddTransient<ISendFromCloudToHl7Service, SendFromCloudToHl7Service>();
            services.AddTransient<IPostResponseCloudService, PostResponseCloudService>();
            services.AddTransient<IPostCompletionCloudService, PostCompletionCloudService>();
            services.AddTransient<ISendToCloudService, SendToCloudService>();
            services.AddTransient<ICloudAgentTaskLoader, CloudAgentTaskLoader>();            
            services.AddTransient<IMarkDownloadCompleteService, MarkDownloadCompleteService>();
            services.AddTransient<ICloudConnectionCacheAccessor, CloudConnectionCacheAccessor>();
            services.AddTransient<ICloudConnectionCacheManager, CloudConnectionCacheManager>();

            // dicom features
            services.AddTransient<IDicomCFindCommand, DicomCFindCommand>();
            services.AddTransient<IDicomCEchoCommand, DicomCEchoCommand>();
            services.AddTransient<IDicomCMoveCommand, DicomCMoveCommand>();
            services.AddTransient<IDicomCGetCommand, DicomCGetCommand>();
            services.AddTransient<IDicomCStoreCommand, DicomCStoreCommand>();
            services.AddTransient<ISendToDicomService, SendToDicomService>();

            // HL7 features
            services.AddTransient<IAckMessageFormatter, AckMessageFormatter>();
            services.AddTransient<IHl7ReaderService, Hl7ReaderService>();
            services.AddTransient<ISendToHl7Service, SendToHl7Service>();
            services.AddTransient<IHl7AcceptService, Hl7AcceptService>();
            services.AddTransient<IHl7StartService, Hl7StartService>();
            services.AddTransient<IHl7ClientsCleaner, Hl7ClientsCleaner>();

            // dcmtk features
            services.AddTransient<IDcmtkConnectionInitializer, DcmtkConnectionInitializer>();
            services.AddTransient<IDcmSendService, DcmSendService>();
            services.AddTransient<IDcmtkDumpService, DcmtkDumpService>();
            services.AddTransient<IDcmtkScanner, DcmtkScanner>();
            services.AddTransient<IDicomizeService, DicomizeService>();
            services.AddTransient<IEchoSCUService, EchoSCUService>();
            services.AddTransient<IFindSCUService, FindSCUService>();
            services.AddTransient<IMoveSCUService, MoveSCUService>();
            services.AddTransient<IPushToDicomService, PushToDicomService>();
            services.AddTransient<IStoreScpService, StoreScpService>();

            // lite connection features
            services.AddTransient<ILiteUploadService, LiteUploadService>();
            services.AddTransient<ILiteToEgsService, LiteToEgsService>();
            services.AddTransient<ILitePresentAsResourceService, LitePresentAsResourceService>();
            services.AddTransient<ILiteStoreService, LiteStoreService>();
            services.AddTransient<IGetLiteReresourcesService, GetLiteReresourcesService>();
            services.AddTransient<ILitePingService, LitePingService>();
            services.AddTransient<ILiteDownloadService, LiteDownloadService>();            
            services.AddTransient<IDownloadViaHttpService, DownloadViaHttpService>();
            services.AddTransient<IDeleteEGSResourceService, DeleteEGSResourceService>();
            services.AddTransient<ILiteConnectionPurgeService, LiteConnectionPurgeService>();
            services.AddTransient<IRegisterWithEGSService, RegisterWithEGSService>();
            services.AddTransient<ISendToAllHubsService, SendToAllHubsService>();            

            // file manager features
            services.AddTransient<IFilePathFormatterHelper, FilePathFormatterHelper>();
            services.AddTransient<ISendFileService, SendFileService>();
            services.AddTransient<IFileExpanderService, FileExpanderService>();
            services.AddTransient<IFileScanService, FileScanService>();

            // infrastructure            
            services.AddSingleton<ILiteTaskUpdater, LiteTaskUpdater>();
            services.AddSingleton<ILITETask, LITETask>();
            services.AddSingleton<ILiteEngine, LiteEngine>();

            var serviceProvider = services.BuildServiceProvider();

            ServiceActivator.Configure(serviceProvider);

            return serviceProvider;
        }
    }
}
