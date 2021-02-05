using Lite.Core.Connections;
using Lite.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Lite.Services.Connections.Dcmtk.Features
{
    public interface IStoreScpService
    {

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
        Task<bool> StoreScp(int taskID, DcmtkConnection connection);
    }

    public sealed class StoreScpService : DcmtkFeatureBase, IStoreScpService
    {
        private readonly IProfileStorage _profileStorage;
        private readonly ILITETask _taskManager;
        private readonly IUtil _util;

        public StoreScpService(
            IProfileStorage profileStorage,
            IUtil util,
            ILITETask taskManager,
            ILogger<FindSCUService> logger) : base(logger)
        {
            _profileStorage = profileStorage;
            _taskManager = taskManager;
            _util = util;
        }

        public override DcmtkConnection Connection { get; set; }

        public async Task<bool> StoreScp(int taskID, DcmtkConnection connection)
        {
            Connection = connection;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var proc = new Process();
            var procinfo = new ProcessStartInfo();

            var taskInfo = $"task: {taskID} connection: {Connection.name}";
            var profile = _profileStorage.Current;

            try
            {
                var dir = profile.tempPath + Path.DirectorySeparatorChar + Connection.name + Path.DirectorySeparatorChar + "toScanner";
                Directory.CreateDirectory(dir);
                string args = $"-xf \"{Connection.storescpCfgFile}\" Default -od \"{dir}\" -aet {Connection.localAETitle} {Connection.localPort}";

                procinfo.UseShellExecute = false;
                procinfo.RedirectStandardError = true;
                procinfo.RedirectStandardOutput = true;
                procinfo.CreateNoWindow = true;
                if (profile.dcmtkLibPath != null)
                {
                    procinfo.WorkingDirectory = profile.dcmtkLibPath;
                    procinfo.FileName = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "storescp";
                    var DCMDICTPATH = profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "dicom.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "acrnema.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "diconde.dic";
                    DCMDICTPATH += _util.EnvSeparatorChar() + profile.dcmtkLibPath + Path.DirectorySeparatorChar + "share" + Path.DirectorySeparatorChar + "dcmtk" + Path.DirectorySeparatorChar + "private.dic";
                    procinfo.Environment.Add("DCMDICTPATH", DCMDICTPATH);
                }
                else
                {
                    procinfo.FileName = "storescp";
                }
                procinfo.Arguments = args;
                proc.StartInfo = procinfo;

                proc.OutputDataReceived += OutputHandler;
                proc.ErrorDataReceived += ErrorHandler;
                proc.EnableRaisingEvents = true;
                proc.Exited += OnProcExit;

                _logger.Log(LogLevel.Information, $"{taskInfo} starting {procinfo.FileName} {procinfo.Arguments}");

                if (proc.Start())
                {
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} is listening on {procinfo.Arguments}...");

                    while (!proc.HasExited)
                    {
                        await Task.Delay(10000, _taskManager.cts.Token).ConfigureAwait(false);
                        if (_taskManager.cts.IsCancellationRequested)
                        {
                            //proc.Kill();
                            _logger.Log(LogLevel.Debug, $"{taskInfo} {procinfo.FileName} is not killed due to potential crash.Trace AMG1");
                        }
                    }

                    if (proc.ExitCode != 0)
                    {
                        _logger.Log(LogLevel.Warning, $"{taskInfo} {procinfo.FileName} ExitCode: {proc.ExitCode}");
                        _logger.Log(LogLevel.Debug, $"{taskInfo} {procinfo.FileName} trace AMG2");
                        return false;
                    }

                    _logger.Log(LogLevel.Information, $"{taskInfo} {procinfo.FileName} status: {proc.ExitCode} elapsed: {stopWatch.Elapsed}");

                    return true;
                }

                return false;
            }
            catch (TaskCanceledException)
            {
                _logger.Log(LogLevel.Information, $"Task was canceled.");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogFullException(e);                
                return false;
            }
            finally
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
                catch (Exception)
                {
                    //eat it
                }

                _taskManager.Stop($"{Connection.name}.storescp");
            }
        }
    }
}
