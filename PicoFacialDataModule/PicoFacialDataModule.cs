using Microsoft.Extensions.Logging;
using PicoFacialDataModule.Models;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Expressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PicoFacialDataModule
{
    public class PicoFacialDataModule : ExtTrackingModule
    {
        private const int PORT = 9030;
        private const string MULTICAST_ADDRESS = "239.255.255.250";

        private const string DISCOVER_PAYLOAD = "DISCOVER_DAEMON";

        private const string PING = "MARCO";
        private const string REPLY = "POLO";

        private const string STOP = "STOP";

        // Fork-only split protocol: a one-byte tag followed by only the fields the module reads.
        private const byte EYE_TAG = (byte)'E';
        private const byte FACE_TAG = (byte)'F';
        private const int EYE_FRAME_LENGTH = 1 + 72;
        private const int FACE_FRAME_LENGTH = 1 + 224;

        private UdpClient? _udpClient;
        private IPEndPoint? _client;
        private bool _established;
        private BridgeMode _mode = BridgeMode.Normal;

        // Latest facial frame, kept so an eye frame can still resolve its blink fallback, EyeWide,
        // EyeSquint and brow blendshapes (which only update at the slower facial rate).
        private PicoFTInfo _face;
        private bool _faceReceived;
        // About six facial frames at 23 Hz; tolerates scheduling jitter, never replays indefinitely.
        private const ulong FACE_CACHE_MAX_AGE_NS = 250_000_000;

#pragma warning disable CS8618 // Because we didn't initialize in the constructor it is WHINING!
        private FaceTrackingParser _faceTrackingParser;
        private EyeTrackingParser _eyeTrackingParser;
        private ModuleSettings _moduleSettings;
#pragma warning restore CS8618

#if EYEDEBUG || FACEDEBUG
        private int _consolePixels;
#endif

        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, false);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            try
            {
                ModuleInformation.Name = "Pico 4 P/E Facial Tracking Daemon";
                ModuleInformation.Active = true;

                var stream = GetType().Assembly.GetManifestResourceStream("PicoFacialDataModule.Assets.icon.png");

                ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

                _udpClient = new UdpClient(PORT)
                {
                    EnableBroadcast = true,
                    MulticastLoopback = false,
                };

                _udpClient.Client.ReceiveTimeout = 2000;

                _moduleSettings = SettingsManager.GetOrCreate();

                _faceTrackingParser = new FaceTrackingParser(_moduleSettings);
                _eyeTrackingParser = new EyeTrackingParser(_moduleSettings);
                LogShapeGain();

                return (!_moduleSettings.DisableEyeTracking, !_moduleSettings.DisableFaceTracking);
            } catch (Exception e)
            {
                Logger.LogCritical($"Initialization failed with the following message: {e.Message}\n Stacktrace:\n{e.StackTrace}");
                return (false, false);
            }
        }

        /// <summary>Logs the effective eye-shape gains once at startup, only when the stage is enabled.</summary>
        private void LogShapeGain()
        {
            if (!_moduleSettings.ShapeGain.Enabled)
                return;

            var gain = new ShapeGain(_moduleSettings);
            Logger.LogInformation($"Shape gain enabled: default={_moduleSettings.ShapeGain.DefaultGain} "
                + $"EyeWideLeft={gain.GainFor(UnifiedExpressions.EyeWideLeft)} EyeWideRight={gain.GainFor(UnifiedExpressions.EyeWideRight)} "
                + $"EyeSquintLeft={gain.GainFor(UnifiedExpressions.EyeSquintLeft)} EyeSquintRight={gain.GainFor(UnifiedExpressions.EyeSquintRight)} "
                + $"(scaled weights clamp to {ShapeGain.MaxValue})");
        }

        public override void Update()
        {
#if EYEDEBUG || FACEDEBUG
            var currentConsolePixels = Console.WindowWidth + Console.WindowHeight;
            if (_consolePixels != currentConsolePixels)
            {
                Console.Clear();
                _consolePixels = currentConsolePixels;
            }
#endif

            if (!ModuleInformation.Active)
            {
                Thread.Sleep(500);
                return;
            }

            try
            {
                if (!_established)
                {
                    byte[]? initialResult = Start();

                    _established = true;
                    ProcessReply(initialResult);
                }

                byte[]? result = null;

                try
                {
                    IPEndPoint? receiver = null;
                    result = _udpClient!.Receive(ref receiver);
                }
                catch
                {
                    ResetSession();
                }

                ProcessReply(result);

            } catch (Exception e)
            {
                Logger.LogCritical($"The module failed with the following exception: {e.Message}\n Stacktrace:\n{e.StackTrace}");

                // Good night!
                Thread.Sleep(Timeout.Infinite);
            }
        }

        public override void Teardown()
        {
            if (_udpClient != null && _client != null)
                _udpClient.Send(Encoding.UTF8.GetBytes(STOP), _client);

            if (_udpClient != null)
                _udpClient.Dispose();
        }

        private void ProcessReply(byte[]? result)
        {
            if (result == null)
                return;

            // The bridge advertises its mode on a separate ASCII control datagram.
            if (BridgeMode.TryParse(result, out var advertised))
            {
                UpdateMode(advertised);
                return;
            }

            // Keep-alive ping.
            if (result.Length == PING.Length + 1)
            {
                _udpClient!.Send(Encoding.UTF8.GetBytes(REPLY), _client);
                return;
            }

            if (result.Length == FACE_FRAME_LENGTH && result[0] == FACE_TAG)
            {
                if (MemoryMarshal.TryRead<PicoFTInfo>(result.AsSpan(1), out var picoFTInfo))
                {
                    // Cache unconditionally: the eye parser reads blendshapes even when facial
                    // expression output is disabled.
                    _face = picoFTInfo;
                    _faceReceived = true;

                    if (!_moduleSettings!.DisableFaceTracking)
                        _faceTrackingParser!.Parse(picoFTInfo);
                }

                return;
            }

            if (result.Length == EYE_FRAME_LENGTH && result[0] == EYE_TAG)
            {
                if (!_moduleSettings!.DisableEyeTracking
                    && MemoryMarshal.TryRead<PxrEyePoseDataV2>(result.AsSpan(1), out var eyeData))
                {
                    ulong distance = eyeData.Timestamp >= _face.Timestamp
                        ? eyeData.Timestamp - _face.Timestamp : _face.Timestamp - eyeData.Timestamp;
                    var faceInfo = _faceReceived && distance <= FACE_CACHE_MAX_AGE_NS ? _face : default;

                    _eyeTrackingParser!.Parse(eyeData, faceInfo, _mode);
                }

                return;
            }
        }

        private void ResetSession()
        {
            _established = false;
            _faceReceived = false;
            _face = default;
            _mode = BridgeMode.Normal;
        }

        /// <summary>
        /// Stores the mode advertised by the bridge, logging and re-labelling the module when it changes.
        /// </summary>
        private void UpdateMode(BridgeMode advertised)
        {
            if (advertised.Enhance == _mode.Enhance
                && advertised.Plugin == _mode.Plugin
                && advertised.Rooted == _mode.Rooted
                && advertised.GateOn == _mode.GateOn
                && advertised.EnhanceModule == _mode.EnhanceModule)
                return;

            _mode = advertised;

            Logger.LogInformation($"Bridge mode changed: {advertised}");

            ModuleInformation.Name = advertised.Enhance
                ? "Pico 4 P/E Facial Tracking Daemon (Enhanced)"
                : "Pico 4 P/E Facial Tracking Daemon";
        }

        /// <summary>
        /// Wakes up the peer daemon by sending a ping, and waiting for a reply.
        /// The daemon will shut down automatically once the UDP port gets disposed.
        /// </summary>
        /// <returns></returns>
        private byte[] Start()
        {
            ResetSession();
            IPEndPoint endpoint = new IPEndPoint(
                string.IsNullOrEmpty(_moduleSettings.IP) ? IPAddress.Parse(MULTICAST_ADDRESS) : IPAddress.Parse(_moduleSettings.IP), 
                PORT
            );

            var discoverPayload = Encoding.UTF8.GetBytes(DISCOVER_PAYLOAD);

            byte[]? reply = null;

            // Get all network cards.
            var networkIPs = Dns.GetHostAddresses(Dns.GetHostName()).Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            while (true)
            {
                foreach (var IP in networkIPs)
                {
                    _udpClient!.Client.SetSocketOption(
                        SocketOptionLevel.IP,
                        SocketOptionName.MulticastInterface,
                        IP.GetAddressBytes()
                     );
                    _udpClient.Send(discoverPayload, discoverPayload.Length, endpoint);
                }

                IPEndPoint? receiver = null;

                try
                {
                    reply = _udpClient!.Receive(ref receiver);
                }
                catch { }

                if (reply != null)
                {
                    _client = receiver!;
                    return reply;
                }
            }
        }
    }
}
