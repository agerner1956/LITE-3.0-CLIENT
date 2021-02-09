using Lite.Core;
using Lite.Core.Connections;
using Lite.Core.Enums;
using Lite.Core.Interfaces;
using Lite.Core.Models;
using Lite.Core.Utils;
using Lite.Services.Connections.Dcmtk.Features;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk
{
    public interface IDcmtkConnectionManager : IConnectionManager
    {
    }

    public class DcmtkConnectionManager : ConnectionManager<DcmtkConnection>, IDcmtkConnectionManager
    {
        [NonSerialized()]
        private readonly SemaphoreSlim toDicomSignal = new SemaphoreSlim(0, 1);

        private readonly IDcmtkConnectionInitializer _connectionInitializer;
        private readonly IDicomUtil _dicomUtil;
        private readonly IDcmtkScanner _scanner;
        private readonly IStoreScpService _storeScpService;
        private readonly IEchoSCUService _echoSCUService;
        private readonly IPushToDicomService _pushToDicomService;

        public DcmtkConnectionManager(
            IDcmtkConnectionInitializer connectionInitializer,
            IProfileStorage profileStorage,
            ILiteConfigService liteConfigService,
            IRoutedItemManager routedItemManager,
            IRulesManager rulesManager,            
            IDicomUtil dicomUtil,
            IDcmtkScanner scanner,
            IStoreScpService storeScpService,
            IEchoSCUService echoSCUService,
            IPushToDicomService pushToDicomService,
            ILITETask taskManager,
            IUtil util,
            ILogger<DcmtkConnectionManager> logger)
            : base(profileStorage, liteConfigService, routedItemManager, null, rulesManager, taskManager, logger, util)
        {
            _connectionInitializer = connectionInitializer;
            _scanner = scanner;
            _dicomUtil = dicomUtil;
            _storeScpService = storeScpService;
            _echoSCUService = echoSCUService;
            _pushToDicomService = pushToDicomService;
        }

        protected override void ProcessImpl(Connection connection)
        {
            base.ProcessImpl(connection);

            Connection.storescpCfgFile = _util.GetTempFolder() + Path.DirectorySeparatorChar + Constants.Dirs.Profiles + Path.DirectorySeparatorChar + "storescp.cfg";
            Connection.toDicom.CollectionChanged += ToDicomCollectionChanged;
        }

        protected virtual void ToDicomCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _logger.Log(LogLevel.Debug, $"connection: {Connection.name}");

                // Signal event to wake up and process queues if awaiting data
                try
                {
                    if (toDicomSignal.CurrentCount == 0) toDicomSignal.Release();
                }
                catch (Exception) { }  //could be in the middle of being disposed and recreated
            }
        }

        public async Task<bool> EchoSCU(int taskID)
        {
            return await _echoSCUService.EchoSCU(taskID, Connection);
        }

        public override void Init()
        {
            _connectionInitializer.Init(this);
            Connection.started = true;
        }

        public override async Task Kickoff(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            _logger.Log(LogLevel.Debug, $"{taskInfo} Beginning Tasks");
            var profile = _profileStorage.Current;

            try
            {
                if (Connection.TestConnection)
                {
                    if (LITETask.CanStart($"{Connection.name}.echoscu") && (Connection.inout == InOut.outbound | Connection.inout == InOut.both))
                    {
                        var newTaskID = LITETask.NewTaskID();
                        Task task = new Task(new Action(async () => await EchoSCU(newTaskID)));
                        await LITETask.Start(newTaskID, task, $"{Connection.name}.echoscu", isLongRunning: true);
                    }
                }

                if (LITETask.CanStart($"{Connection.name}.PushtoDicomEventLoop"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await PushtoDicomEventLoop(newTaskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.PushtoDicomEventLoop", isLongRunning: false);
                }

                if (LITETask.CanStart($"{Connection.name}.storescp") && (Connection.inout == InOut.inbound | Connection.inout == InOut.both))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await StoreScp(newTaskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.storescp", isLongRunning: true);
                }

                if (LITETask.CanStart($"{Connection.name}.SendToRules"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await SendToRules(newTaskID, Connection.responsive)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.SendToRules", isLongRunning: true);
                }

                if (LITETask.CanStart($"{Connection.name}.Scanner"))
                {
                    var newTaskID = LITETask.NewTaskID();
                    Task task = new Task(new Action(async () => await Scanner(newTaskID)));
                    await LITETask.Start(newTaskID, task, $"{Connection.name}.Scanner", isLongRunning: false);
                }

                await Task.Delay(profile.KickOffInterval, LITETask.cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.Kickoff");
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} Ending Tasks");
        }

        public override void Stop()
        {
            Connection.started = false;
        }

#pragma warning disable 1998
        public override RoutedItem Route(RoutedItem routedItem, bool copy = false)
        {
            //enqueue the routedItem, we don't support Q/R at this stage of dev
            if (routedItem.sourceFileName != null)
            {
                //check if dicom, if not dicomize since dicom only does dicom, duh.
                if (!_dicomUtil.IsDICOM(routedItem))
                {
                    routedItem = _dicomUtil.Dicomize(routedItem);
                }
            }

            _routedItemManager.Init(routedItem);
            _routedItemManager.Enqueue(Connection, Connection.toDicom, nameof(Connection.toDicom), copy: copy);

            return routedItem;
        }
#pragma warning restore 1998

        public async Task PushtoDicomEventLoop(int taskID)
        {
            var taskInfo = $"task: {taskID} connection: {Connection.name}";

            var profile = _profileStorage.Current;

            _logger.Log(LogLevel.Debug, $"{taskInfo} Beginning PushtoDicom");

            try
            {
                await PushtoDicom(taskID);
                bool success = await toDicomSignal.WaitAsync(profile.KickOffInterval, LITETask.cts.Token).ConfigureAwait(false);
                //toDicomSignal.Release();
                // toDicomSignal.Dispose();
                // toDicomSignal = new SemaphoreSlim(0, 1);

            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
            }
            catch (Exception e)
            {
                _logger.LogFullException(e, taskInfo);
            }
            finally
            {
                LITETask.Stop($"{Connection.name}.PushtoDicomEventLoop");
            }

            _logger.Log(LogLevel.Debug, $"{taskInfo} Ending PushtoDicomEventLoop");
        }

        public async Task PushtoDicom(int taskID)
        {
            await _pushToDicomService.PushtoDicom(taskID, Connection);
        }

        public async Task Scanner(int taskID)
        {            
            await _scanner.Scanner(taskID, Connection);
        }

        /**

storescp is used for the listening side of the dcmtkconnection.  Below are the possible parameters
we can send.  We just need to wire these up to the profile to send what is required.

storescp: DICOM storage (C-STORE) SCP
usage: storescp [options] [port]

parameters:
          port                           tcp/ip port number to listen on

general options:
          -h      --help                 print this help text and exit
                  --version              print version information and exit
                  --arguments            print expanded command line arguments
          -q      --quiet                quiet mode, print no warnings and errors
          -v      --verbose              verbose mode, print processing details
          -d      --debug                debug mode, print debug information
          -ll     --log-level            [l]evel: string constant
                                         (fatal, error, warn, info, debug, trace)
                                         use level l for the logger
          -lc     --log-config           [f]ilename: string
                                         use config file f for the logger
          +v      --verbose-pc           show presentation contexts in verbose mode
multi-process options:
                  --single-process       single process mode (default)
                  --fork                 fork child process for each association
network options:
          association negotiation profile from configuration file:
            -xf   --config-file          [f]ilename, [p]rofile: string
                                         use profile p from config file f
          preferred network transfer syntaxes (not with --config-file):
            +x=   --prefer-uncompr       prefer explicit VR local byte order (default)
            +xe   --prefer-little        prefer explicit VR little endian TS
            +xb   --prefer-big           prefer explicit VR big endian TS
            +xs   --prefer-lossless      prefer default JPEG lossless TS
            +xy   --prefer-jpeg8         prefer default JPEG lossy TS for 8 bit data
            +xx   --prefer-jpeg12        prefer default JPEG lossy TS for 12 bit data
            +xv   --prefer-j2k-lossless  prefer JPEG 2000 lossless TS
            +xw   --prefer-j2k-lossy     prefer JPEG 2000 lossy TS
            +xt   --prefer-jls-lossless  prefer JPEG-LS lossless TS
            +xu   --prefer-jls-lossy     prefer JPEG-LS lossy TS
            +xm   --prefer-mpeg2         prefer MPEG2 Main Profile @ Main Level TS
            +xh   --prefer-mpeg2-high    prefer MPEG2 Main Profile @ High Level TS
            +xn   --prefer-mpeg4         prefer MPEG4 AVC/H.264 HP / Level 4.1 TS
            +xl   --prefer-mpeg4-bd      prefer MPEG4 AVC/H.264 BD-compatible TS
            +x2   --prefer-mpeg4-2-2d    prefer MPEG4 AVC/H.264 HP / Level 4.2 TS (2D)
            +x3   --prefer-mpeg4-2-3d    prefer MPEG4 AVC/H.264 HP / Level 4.2 TS (3D)
            +xo   --prefer-mpeg4-2-st    prefer MPEG4 AVC/H.264 Stereo HP / Level 4.2 TS
            +x4   --prefer-hevc          prefer HEVC/H.265 Main Profile / Level 5.1 TS
            +x5   --prefer-hevc10        prefer HEVC/H.265 Main 10 Profile / Level 5.1 TS
            +xr   --prefer-rle           prefer RLE lossless TS
            +xd   --prefer-deflated      prefer deflated expl. VR little endian TS
            +xi   --implicit             accept implicit VR little endian TS only
            +xa   --accept-all           accept all supported transfer syntaxes
          other network options:
            -id   --inetd                run from inetd super server (not with --fork)
            -ts   --socket-timeout       [s]econds: integer (default: 60)
                                         timeout for network socket (0 for none)
            -ta   --acse-timeout         [s]econds: integer (default: 30)
                                         timeout for ACSE messages
            -td   --dimse-timeout        [s]econds: integer (default: unlimited)
                                         timeout for DIMSE messages
            -aet  --aetitle              [a]etitle: string
                                         set my AE title (default: STORESCP)
            -pdu  --max-pdu              [n]umber of bytes: integer (4096..131072)
                                         set max receive pdu to n bytes (default: 16384)
            -dhl  --disable-host-lookup  disable hostname lookup
                  --refuse               refuse association
                  --reject               reject association if no implement. class UID
                  --ignore               ignore store data, receive but do not store
                  --sleep-after          [s]econds: integer
                                         sleep s seconds after store (default: 0)
                  --sleep-during         [s]econds: integer
                                         sleep s seconds during store (default: 0)
                  --abort-after          abort association after receipt of C-STORE-RQ
                                         (but before sending response)
                  --abort-during         abort association during receipt of C-STORE-RQ
            -pm   --promiscuous          promiscuous mode, accept unknown SOP classes
                                         (not with --config-file)
            -up   --uid-padding          silently correct space-padded UIDs
output options:
          general:
            -od   --output-directory     [d]irectory: string (default: ".")
                                         write received objects to existing directory d
          bit preserving mode:
            -B    --normal               allow implicit format conversions (default)
            +B    --bit-preserving       write data exactly as read
          output file format:
            +F    --write-file           write file format (default)
            -F    --write-dataset        write data set without file meta information
          output transfer syntax (not with --bit-preserving or compressed transmission):
            +t=   --write-xfer-same      write with same TS as input (default)
            +te   --write-xfer-little    write with explicit VR little endian TS
            +tb   --write-xfer-big       write with explicit VR big endian TS
            +ti   --write-xfer-implicit  write with implicit VR little endian TS
            +td   --write-xfer-deflated  write with deflated expl. VR little endian TS
          post-1993 value representations (not with --bit-preserving):
            +u    --enable-new-vr        enable support for new VRs (UN/UT) (default)
            -u    --disable-new-vr       disable support for new VRs, convert to OB
          group length encoding (not with --bit-preserving):
            +g=   --group-length-recalc  recalculate group lengths if present (default)
            +g    --group-length-create  always write with group length elements
            -g    --group-length-remove  always write without group length elements
          length encoding in sequences and items (not with --bit-preserving):
            +e    --length-explicit      write with explicit lengths (default)
            -e    --length-undefined     write with undefined lengths
          data set trailing padding (not with --write-dataset or --bit-preserving):
            -p    --padding-off          no padding (default)
            +p    --padding-create       [f]ile-pad [i]tem-pad: integer
                                         align file on multiple of f bytes and items
                                         on multiple of i bytes
          deflate compression level (only with --write-xfer-deflated/same):
            +cl   --compression-level    [l]evel: integer (default: 6)
                                         0=uncompressed, 1=fastest, 9=best compression
          sorting into subdirectories (not with --bit-preserving):
            -ss   --sort-conc-studies    [p]refix: string
                                         sort studies using prefix p and a timestamp
            -su   --sort-on-study-uid    [p]refix: string
                                         sort studies using prefix p and the Study
                                         Instance UID
            -sp   --sort-on-patientname  sort studies using the Patient's Name and
                                         a timestamp
          filename generation:
            -uf   --default-filenames    generate filename from instance UID (default)
            +uf   --unique-filenames     generate unique filenames
            -tn   --timenames            generate filename from creation time
            -fe   --filename-extension   [e]xtension: string
                                         append e to all filenames
event options:
          -xcr    --exec-on-reception    [c]ommand: string
                                         execute command c after having received and
                                         processed one C-STORE-RQ message
          -xcs    --exec-on-eostudy      [c]ommand: string
                                         execute command c after having received and
                                         processed all C-STORE-RQ messages that belong
                                         to one study
          -rns    --rename-on-eostudy    having received and processed all C-STORE-RQ
                                         messages that belong to one study, rename
                                         output files according to certain pattern
          -tos    --eostudy-timeout      [t]imeout: integer
                                         specifies a timeout of t seconds for
                                         end-of-study determination
          -xs     --exec-sync            execute command synchronously in foreground

          */
        public async Task<bool> StoreScp(int taskID)
        {
            return await _storeScpService.StoreScp(taskID, Connection);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                toDicomSignal.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
