using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Speech
{
    /// <summary>
    /// Linux speech output for the game running under Wine or Proton. Prism's backends are
    /// Windows screen readers and SAPI; under Wine none of them reaches the user's Linux screen
    /// reader, so this channel talks to speech-dispatcher — the speech server Orca uses — instead.
    ///
    /// Wine cannot open Unix sockets from Windows code, but it does run native Linux programs
    /// and its TCP sockets are real ones. So, in order:
    /// <list type="number">
    /// <item>SSIP over TCP to <c>127.0.0.1:6560</c> (or <c>SPEECHD_ADDRESS=inet_socket:host:port</c>,
    /// or <c>AA_SPEECHD_PORT</c>): either speech-dispatcher itself in inet mode or the bridge
    /// already running as a user service.</item>
    /// <item>An nvda2speechd server on <c>ws://127.0.0.1:3457</c> (or <c>NVDA2SPEECHD_HOST</c>),
    /// for users who already run it for other Windows games.</item>
    /// <item>If nothing answers, the mod starts the bundled bridge (<c>speechd-bridge.py</c>,
    /// TCP → speech-dispatcher's Unix socket) with the system Python and connects to it.</item>
    /// <item>Last resort: one <c>spd-say</c> process per announcement.</item>
    /// </list>
    ///
    /// All I/O happens on a background thread, so a slow or dead speech server can never stall
    /// the game's main thread. Failure policy matches <see cref="ScreenReaderOutput"/>: never
    /// throw at a call site.
    /// </summary>
    internal sealed class SpeechDispatcherOutput
    {
        internal const string BackendName = "Speech Dispatcher";

        private const string LogTag = "Speech";
        private const string BridgeResourceName = "linux.speechd-bridge.py";
        private const string BridgeFileName = "AccessibleArena-speechd-bridge.py";
        private const int ConnectTimeoutMs = 1500;
        private const int ReplyTimeoutMs = 3000;
        private const int BridgeIdleExitSeconds = 30;
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(3);

        private enum Mode { None, Ssip, Nvda2Speechd, SpdSay }

        private enum CommandKind { Speak, Cancel }

        private struct Command
        {
            public CommandKind Kind;
            public string Text;
        }

        private readonly object _queueLock = new object();
        private readonly Queue<Command> _queue = new Queue<Command>();
        private Thread _worker;
        private volatile bool _running;

        private Mode _mode = Mode.None;
        private string _host = "127.0.0.1";
        private int _port = SsipProtocol.DefaultPort;
        private string _wsHost = "127.0.0.1";
        private int _wsPort = Nvda2SpeechdProtocol.DefaultPort;
        private string _wsPath = "/";
        private readonly Random _random = new Random();
        private string _spdSayPath;
        private bool _bridgeLaunched;
        private DateTime _lastConnectAttempt = DateTime.MinValue;

        // Rate from the mod settings (-100..100, 0 = speech-dispatcher's default). Written by
        // the main thread, applied by the worker before its next command.
        private volatile int _rate;
        private volatile bool _rateDirty;
        private bool _rateUnsupportedLogged;

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly List<byte> _lineBuffer = new List<byte>(256);

        /// <summary>Human-readable description of the transport in use, for the log.</summary>
        internal string Transport { get; private set; } = "none";

        internal bool IsRunning => _running;

        /// <summary>
        /// Finds a working way to speech-dispatcher and starts the worker. Returns false when
        /// none exists; the caller then falls back to Prism.
        /// </summary>
        internal bool Start()
        {
            if (_running)
                return true;

            ResolveAddress();

            if (TryConnect())
            {
                _mode = Mode.Ssip;
                Transport = $"SSIP over TCP {_host}:{_port}";
            }
            else if (TryConnectNvda2Speechd())
            {
                _mode = Mode.Nvda2Speechd;
                Transport = $"nvda2speechd server at ws://{_wsHost}:{_wsPort}{_wsPath}";
            }
            else if (IsLocalHost(_host) && TryLaunchBridge() && WaitForBridge())
            {
                _mode = Mode.Ssip;
                Transport = $"SSIP via bundled bridge on {_host}:{_port}";
            }
            else
            {
                _spdSayPath = WineInterop.FindLinuxProgram("/usr/bin/spd-say", "/bin/spd-say",
                                                           "/usr/local/bin/spd-say", "/run/host/usr/bin/spd-say");
                if (_spdSayPath == null)
                {
                    Log.Warn(LogTag, "speech-dispatcher not reachable: nothing on " +
                                     $"{_host}:{_port}, bridge could not start, spd-say not found");
                    return false;
                }

                _mode = Mode.SpdSay;
                Transport = $"spd-say ({_spdSayPath})";
            }

            _running = true;
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "AccessibleArena.SpeechDispatcher" };
            _worker.Start();
            Log.Msg(LogTag, $"speech-dispatcher ready — {Transport}");
            return true;
        }

        /// <summary>Queues an announcement. <paramref name="interrupt"/> drops anything still queued and cancels current speech.</summary>
        internal void Speak(string text, bool interrupt)
        {
            if (!_running || string.IsNullOrEmpty(text))
                return;

            lock (_queueLock)
            {
                if (interrupt)
                {
                    _queue.Clear();
                    _queue.Enqueue(new Command { Kind = CommandKind.Cancel });
                }
                _queue.Enqueue(new Command { Kind = CommandKind.Speak, Text = text });
                Monitor.Pulse(_queueLock);
            }
        }

        /// <summary>
        /// Sets the speech rate, -100 (slowest) to 100 (fastest); 0 keeps speech-dispatcher's
        /// default (or <c>AA_SPEECHD_RATE</c>). Takes effect before the next announcement.
        /// </summary>
        internal void SetRate(int rate)
        {
            _rate = SsipProtocol.ClampRate(rate);
            _rateDirty = true;
        }

        /// <summary>Drops queued announcements and stops current speech.</summary>
        internal void Silence()
        {
            if (!_running)
                return;

            lock (_queueLock)
            {
                _queue.Clear();
                _queue.Enqueue(new Command { Kind = CommandKind.Cancel });
                Monitor.Pulse(_queueLock);
            }
        }

        internal void Stop()
        {
            _running = false;
            lock (_queueLock)
            {
                _queue.Clear();
                Monitor.Pulse(_queueLock);
            }
            CloseConnection(sendQuit: true);
        }

        #region Worker

        private void WorkerLoop()
        {
            while (_running)
            {
                Command command;
                lock (_queueLock)
                {
                    while (_running && _queue.Count == 0)
                        Monitor.Wait(_queueLock);
                    if (!_running)
                        return;
                    command = _queue.Dequeue();
                }

                try
                {
                    if (_rateDirty)
                        ApplyRate();

                    if (_mode == Mode.Ssip || _mode == Mode.Nvda2Speechd)
                        ExecuteOnSocket(command);
                    else if (_mode == Mode.SpdSay)
                        ExecuteSpdSay(command);
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"speech-dispatcher command failed ({ex.GetType().Name}: {ex.Message})");
                }
            }
        }

        /// <summary>Pushes <see cref="_rate"/> to the live SSIP connection. Worker thread only.</summary>
        private void ApplyRate()
        {
            _rateDirty = false;
            if (_mode == Mode.Nvda2Speechd)
            {
                // nvda2speechd's protocol only has speak and cancel.
                if (!_rateUnsupportedLogged)
                    Log.Msg(LogTag, "nvda2speechd cannot change the speech rate; set it in nvda2speechd or spd-conf");
                _rateUnsupportedLogged = true;
                return;
            }
            if (_mode != Mode.Ssip || _stream == null)
                return; // spd-say passes it per call; a new connection applies it in Handshake

            try
            {
                SendRate();
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException)
            {
                // The next command reconnects, and the handshake applies the rate.
                CloseConnection(sendQuit: false);
                _lastConnectAttempt = DateTime.MinValue;
            }
        }

        private void SendRate()
        {
            int code = SendCommand("SET SELF RATE " + _rate);
            if (!SsipProtocol.IsSuccess(code))
                Log.Warn(LogTag, $"speech-dispatcher rejected rate {_rate} (code {code})");
        }

        private void ExecuteOnSocket(Command command)
        {
            // One retry on a fresh connection: the server may have restarted since the last
            // announcement (Orca restarts speech-dispatcher when its settings change).
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (_stream == null && !Reconnect())
                    return;

                try
                {
                    if (_mode == Mode.Nvda2Speechd)
                        SendWebSocketBinary(command.Kind == CommandKind.Cancel
                            ? Nvda2SpeechdProtocol.EncodeCancel()
                            : Nvda2SpeechdProtocol.EncodeSpeak(command.Text));
                    else if (command.Kind == CommandKind.Cancel)
                        SendCommand("CANCEL SELF");
                    else
                        SendSpeak(command.Text);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException)
                {
                    Log.Msg(LogTag, $"speech-dispatcher connection lost ({ex.Message}); reconnecting");
                    CloseConnection(sendQuit: false);
                    _lastConnectAttempt = DateTime.MinValue;
                }
            }
        }

        private void ExecuteSpdSay(Command command)
        {
            string args = command.Kind == CommandKind.Cancel
                ? "-C"
                : "-P message " + (_rate != 0 ? "-r " + _rate + " " : "") + "-- " + SsipProtocol.QuoteArgument(command.Text.Replace('\r', ' ').Replace('\n', ' '));

            using (Process process = StartHidden(_spdSayPath, args))
            {
                // Waiting keeps announcements in order: spd-say returns once the message is
                // queued in speech-dispatcher, not when it has been spoken.
                if (process != null && !process.WaitForExit(ReplyTimeoutMs))
                    Log.Msg(LogTag, "spd-say did not return in time");
            }
        }

        #endregion

        #region SSIP connection

        private void ResolveAddress()
        {
            string host;
            int port;
            if (SsipProtocol.TryParseInetAddress(WineInterop.SafeEnv("SPEECHD_ADDRESS"), out host, out port))
            {
                _host = host;
                _port = port;
            }

            int overridePort;
            if (int.TryParse(WineInterop.SafeEnv("AA_SPEECHD_PORT"), out overridePort) && overridePort > 0 && overridePort < 65536)
                _port = overridePort;

            string wsHost, wsPath;
            int wsPort;
            if (Nvda2SpeechdProtocol.TryParseHost(WineInterop.SafeEnv("NVDA2SPEECHD_HOST"), out wsHost, out wsPort, out wsPath))
            {
                _wsHost = wsHost;
                _wsPort = wsPort;
                _wsPath = wsPath;
            }
        }

        private bool Reconnect()
        {
            if (DateTime.UtcNow - _lastConnectAttempt < ReconnectInterval)
                return false;

            if (_mode == Mode.Nvda2Speechd)
                return TryConnectNvda2Speechd();

            if (TryConnect())
                return true;

            // The bridge we started may have died with the speech server; start it again.
            if (_bridgeLaunched && TryLaunchBridge() && WaitForBridge())
                return true;

            return false;
        }

        /// <summary>Connects to speech-dispatcher (or the bridge) and runs the SSIP handshake.</summary>
        private bool TryConnect()
        {
            return TryOpen(_host, _port, Handshake);
        }

        /// <summary>Connects to an nvda2speechd server and opens its WebSocket.</summary>
        private bool TryConnectNvda2Speechd()
        {
            return TryOpen(_wsHost, _wsPort, WebSocketHandshake);
        }

        private bool TryOpen(string host, int port, Action handshake)
        {
            _lastConnectAttempt = DateTime.UtcNow;
            CloseConnection(sendQuit: false);

            TcpClient client = null;
            try
            {
                client = new TcpClient();
                IAsyncResult pending = client.BeginConnect(host, port, null, null);
                if (!pending.AsyncWaitHandle.WaitOne(ConnectTimeoutMs) || !client.Connected)
                {
                    client.Close();
                    return false;
                }
                client.EndConnect(pending);
                client.NoDelay = true;

                _client = client;
                _stream = client.GetStream();
                _stream.ReadTimeout = ReplyTimeoutMs;
                _stream.WriteTimeout = ReplyTimeoutMs;
                _lineBuffer.Clear();

                handshake();
                return true;
            }
            catch (Exception)
            {
                // Refused (nothing listening) or the peer closed during the handshake — e.g. the
                // bridge could not reach speech-dispatcher's socket.
                CloseConnection(sendQuit: false);
                if (client != null && _client == null)
                    client.Close();
                return false;
            }
        }

        private void Handshake()
        {
            RequireSuccess(SendCommand(SsipProtocol.ClientNameCommand(WineInterop.SafeEnv("USER") ?? WineInterop.SafeEnv("USERNAME"))), "CLIENT_NAME");
            SendCommand("SET SELF PRIORITY message");

            // Optional voice settings. Orca's own rate and voice belong to Orca's connection, so
            // the mod speaks with speech-dispatcher's defaults unless told otherwise here.
            ApplyOptionalSetting("AA_SPEECHD_MODULE", "SET SELF OUTPUT_MODULE {0}");
            ApplyOptionalSetting("AA_SPEECHD_LANGUAGE", "SET SELF LANGUAGE {0}");
            ApplyOptionalSetting("AA_SPEECHD_VOICE", "SET SELF SYNTHESIS_VOICE {0}");
            ApplyOptionalSetting("AA_SPEECHD_RATE", "SET SELF RATE {0}");
            ApplyOptionalSetting("AA_SPEECHD_PITCH", "SET SELF PITCH {0}");
            ApplyOptionalSetting("AA_SPEECHD_VOLUME", "SET SELF VOLUME {0}");

            // The F2 setting wins over AA_SPEECHD_RATE once it is moved off 0.
            _rateDirty = false;
            if (_rate != 0)
                SendRate();
        }

        private void WebSocketHandshake()
        {
            var keyBytes = new byte[16];
            _random.NextBytes(keyBytes);
            WriteRaw(Nvda2SpeechdProtocol.BuildHandshake(_wsHost, _wsPort, _wsPath, Convert.ToBase64String(keyBytes)));

            string status = ReadLine();
            if (status == null || status.IndexOf(" 101", StringComparison.Ordinal) < 0)
                throw new IOException($"nvda2speechd refused the WebSocket upgrade: {status}");

            // Skip the remaining response headers, up to the blank line.
            while (ReadLine().Length > 0)
            {
            }
        }

        private void SendWebSocketBinary(byte[] payload)
        {
            SendWebSocketFrame(0x2, payload);
        }

        private void SendWebSocketFrame(byte opcode, byte[] payload)
        {
            var mask = new byte[4];
            _random.NextBytes(mask);
            byte[] frame = Nvda2SpeechdProtocol.BuildClientFrame(opcode, payload, mask);
            _stream.Write(frame, 0, frame.Length);
        }

        private void ApplyOptionalSetting(string envName, string commandFormat)
        {
            string value = WineInterop.SafeEnv(envName);
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                return;

            int code = SendCommand(string.Format(commandFormat, value.Trim()));
            if (!SsipProtocol.IsSuccess(code))
                Log.Warn(LogTag, $"speech-dispatcher rejected {envName}={value} (code {code})");
        }

        private void SendSpeak(string text)
        {
            string body = SsipProtocol.BuildSpeakBody(text);
            if (body == null)
                return;

            RequireSuccess(SendCommand("SPEAK"), "SPEAK");
            WriteRaw(body);
            int code = ReadReply();
            if (!SsipProtocol.IsSuccess(code))
                Log.Warn(LogTag, $"speech-dispatcher refused an utterance (code {code})");
        }

        private int SendCommand(string command)
        {
            WriteRaw(command + SsipProtocol.Eol);
            return ReadReply();
        }

        private static void RequireSuccess(int code, string what)
        {
            if (!SsipProtocol.IsSuccess(code))
                throw new IOException($"{what} answered {code}");
        }

        private void WriteRaw(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Reads one full reply and returns its code.</summary>
        private int ReadReply()
        {
            while (true)
            {
                string line = ReadLine();
                int code;
                bool isFinal;
                string text;
                if (!SsipProtocol.TryParseReplyLine(line, out code, out isFinal, out text))
                    continue;
                if (isFinal)
                    return code;
            }
        }

        private string ReadLine()
        {
            _lineBuffer.Clear();
            while (true)
            {
                int b = _stream.ReadByte();
                if (b < 0)
                    throw new IOException("speech-dispatcher closed the connection");
                if (b == '\n')
                    break;
                if (b != '\r')
                    _lineBuffer.Add((byte)b);
            }
            return Encoding.UTF8.GetString(_lineBuffer.ToArray());
        }

        private void CloseConnection(bool sendQuit)
        {
            try
            {
                if (sendQuit && _stream != null)
                {
                    if (_mode == Mode.Nvda2Speechd)
                        SendWebSocketFrame(0x8, new byte[0]); // Close
                    else
                        WriteRaw("QUIT" + SsipProtocol.Eol);
                }
            }
            catch (Exception)
            {
                // Shutting down anyway.
            }

            try { _stream?.Close(); } catch (Exception) { }
            try { _client?.Close(); } catch (Exception) { }
            _stream = null;
            _client = null;
        }

        private static bool IsLocalHost(string host)
        {
            return host == "127.0.0.1" || host == "::1" ||
                   string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Bridge

        /// <summary>
        /// Writes the bundled bridge next to MelonLoader's user data and starts it with the
        /// system Python. Wine runs the Linux interpreter directly from its Windows path.
        /// </summary>
        private bool TryLaunchBridge()
        {
            try
            {
                string python = WineInterop.FindLinuxProgram("/usr/bin/python3", "/bin/python3",
                                                             "/usr/local/bin/python3", "/run/host/usr/bin/python3");
                if (python == null)
                {
                    Log.Msg(LogTag, "python3 not found; cannot start the speech bridge");
                    return false;
                }

                string scriptPath = WriteBridgeScript();
                if (scriptPath == null)
                    return false;

                string unixScript = WineInterop.ToUnixPath(scriptPath);
                if (unixScript == null)
                {
                    Log.Msg(LogTag, $"cannot map {scriptPath} to a Linux path; cannot start the speech bridge");
                    return false;
                }

                string args = $"{SsipProtocol.QuoteArgument(unixScript)} --host {_host} --port {_port} " +
                              $"--idle-exit {BridgeIdleExitSeconds}";
                using (Process process = StartHidden(python, args))
                {
                    if (process == null)
                        return false;
                }

                _bridgeLaunched = true;
                Log.Msg(LogTag, $"started speech bridge: {python} {args}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"could not start the speech bridge ({ex.GetType().Name}: {ex.Message})");
                return false;
            }
        }

        private bool WaitForBridge()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (TryConnect())
                    return true;
                Thread.Sleep(200);
            }
            Log.Msg(LogTag, "speech bridge started but did not answer in time");
            return false;
        }

        private static string WriteBridgeScript()
        {
            Assembly assembly = typeof(SpeechDispatcherOutput).Assembly;
            using (Stream resource = assembly.GetManifestResourceStream(BridgeResourceName))
            {
                if (resource == null)
                {
                    Log.Warn(LogTag, $"embedded resource {BridgeResourceName} missing");
                    return null;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                string userData = Path.Combine(baseDir, "UserData");
                string dir = Directory.Exists(userData) ? userData : baseDir;
                string path = Path.Combine(dir, BridgeFileName);

                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                    resource.CopyTo(file);
                return path;
            }
        }

        private static Process StartHidden(string fileName, string arguments)
        {
            var info = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            return Process.Start(info);
        }

        #endregion
    }
}
