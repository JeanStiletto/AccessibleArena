using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AccessibleArena.Core.Speech;
using AccessibleArena.Core.Utils;

namespace AccessibleArena
{
    /// <summary>
    /// Speech output for the mod, on top of Prism (prism.dll) — the screen-reader abstraction
    /// that replaced Tolk. Prism reaches NVDA, JAWS, ZDSR, PC-Talker, BoyPC Reader, Sense
    /// Reader, ZoomText, Narrator/OneCore, UIA and SAPI from one API, so the mod is no longer
    /// limited to the readers Tolk knew, and it now also speaks when no screen reader is
    /// running at all (Prism falls through to OneCore/SAPI).
    ///
    /// Two channels, mirroring the arrangement the KOTOR accessibility mod validated:
    /// <list type="bullet">
    /// <item><b>Normal</b> — whatever <c>acquire_best</c> picked. Every announcement goes here,
    /// so interrupt behaviour stays the user's own screen reader's behaviour.</item>
    /// <item><b>Urgent</b> — always SAPI. Speech here cannot be swallowed by the screen reader's
    /// own cancel-on-keypress handling. Off by default; the settings menu can route
    /// <see cref="Core.Models.AnnouncementPriority.Critical"/> announcements through it.</item>
    /// </list>
    ///
    /// Failure policy throughout: never throw at a call site. A missing or broken prism.dll
    /// leaves <see cref="IsAvailable"/> false and every method a no-op, exactly as the Tolk
    /// implementation behaved when Tolk.dll was absent.
    /// </summary>
    public static class ScreenReaderOutput
    {
        private const string LogTag = "Speech";

        /// <summary>Backend setting value meaning "let Prism pick by priority".</summary>
        public const string AutoBackend = "auto";

        /// <summary>
        /// SAPI rate for the urgent channel. Prism maps [0.0..1.0] onto SAPI's -10..+10 with
        /// 0.5 = SAPI default, so 0.8 lands around SAPI +6 — clearly faster than stock SAPI
        /// so an alert does not drag, still well inside intelligibility for the bundled voices.
        /// </summary>
        private const float SapiUrgentRate = 0.8f;

        private static readonly object Gate = new object();

        private static bool _initialized;
        private static bool _available;

        private static IntPtr _ctx = IntPtr.Zero;
        private static IntPtr _normal = IntPtr.Zero;   // acquire_best — NVDA / JAWS / OneCore / ...
        private static IntPtr _sapi = IntPtr.Zero;     // urgent channel
        private static bool _sapiReady;

        private static string _normalName = "None";
        private static int _urgentVolumePercent = 100;

        // Optional entry points: older prism.dll builds may not export them, and a backend
        // without the matching SUPPORTS_* feature bit fails at call time. Each is probed once
        // and latched off, so a missing export costs one exception rather than one per call.
        private static bool _canSetVolume = true;
        private static bool _canSetRate = true;

        /// <summary>True once a speech backend is up. False means the mod runs silent.</summary>
        public static bool IsAvailable => _available;

        /// <summary>True when the SAPI urgent channel is usable; false makes urgent fall back to normal.</summary>
        public static bool IsUrgentAvailable => _sapiReady;

        /// <summary>
        /// Brings up Prism and acquires both channels. Safe to call more than once; the second
        /// call reports the first call's result.
        /// </summary>
        public static bool Initialize()
        {
            return Initialize(AutoBackend, 100);
        }

        /// <summary>
        /// Brings up Prism with a stored backend preference and urgent-channel volume.
        /// </summary>
        /// <param name="preferredBackend">Backend name to force, or <see cref="AutoBackend"/>.</param>
        /// <param name="urgentVolumePercent">Urgent-channel volume, 0-100.</param>
        public static bool Initialize(string preferredBackend, int urgentVolumePercent)
        {
            lock (Gate)
            {
                if (_initialized)
                    return _available;

                _initialized = true;
                _urgentVolumePercent = Clamp(urgentVolumePercent, 0, 100);

                try
                {
                    PreloadLibrary();

                    // Reuse the context across a Shutdown/Initialize cycle — prism_shutdown is
                    // deliberately never called (see Shutdown), so a second init would otherwise
                    // strand the first context.
                    if (_ctx == IntPtr.Zero)
                    {
                        var cfg = new PrismInterop.PrismConfig { Version = PrismInterop.prism_config_init() };
                        _ctx = PrismInterop.prism_init(ref cfg);
                    }

                    if (_ctx == IntPtr.Zero)
                    {
                        Log.Warn(LogTag, "prism_init returned NULL; running silent");
                        return false;
                    }

                    AcquireNormal(preferredBackend);
                    AcquireSapi();

                    if (_available)
                    {
                        Log.Msg(LogTag, $"ready — normal backend = {_normalName}, urgent backend = " +
                                        (_sapiReady ? $"SAPI at {_urgentVolumePercent}%"
                                                    : "(SAPI unavailable, urgent falls back to normal)"));
                    }
                    else
                    {
                        Log.Warn(LogTag, "no speech backend available; running silent");
                    }
                }
                catch (DllNotFoundException)
                {
                    Log.Warn(LogTag, $"{PrismInterop.Dll} not found; running silent");
                    _available = false;
                }
                catch (EntryPointNotFoundException ex)
                {
                    Log.Warn(LogTag, $"{PrismInterop.Dll} is missing an expected export ({ex.Message}); running silent");
                    _available = false;
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"Prism init failed ({ex.GetType().Name}: {ex.Message}); running silent");
                    _available = false;
                }

                return _available;
            }
        }

        /// <summary>
        /// Loads prism.dll explicitly from the game folder before the first P/Invoke, so that a
        /// missing file is one clear log line with a Win32 error rather than a
        /// DllNotFoundException from somewhere deeper. Once the module is loaded under this
        /// name, the DllImport declarations bind to it. Failure here is not fatal — the plain
        /// loader search still gets a chance.
        /// </summary>
        private static void PreloadLibrary()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, PrismInterop.Dll);
                if (!File.Exists(path))
                {
                    Log.Msg(LogTag, $"{path} not present; falling back to the default library search path");
                    return;
                }

                if (PrismInterop.LoadLibraryW(path) == IntPtr.Zero)
                    Log.Warn(LogTag, $"LoadLibrary({path}) failed, err={Marshal.GetLastWin32Error()}");
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"prism.dll preload skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// Acquires the normal channel. With <see cref="AutoBackend"/> this is a single
        /// acquire_best call — Prism owns the priority walk and (in the patched build we ship)
        /// skips a backend whose vendor DLL faults during initialize(). A named preference is
        /// tried first and falls back to acquire_best if that backend is gone or refuses.
        /// </summary>
        private static void AcquireNormal(string preferredBackend)
        {
            _normal = IntPtr.Zero;
            _available = false;

            if (!string.IsNullOrEmpty(preferredBackend) &&
                !string.Equals(preferredBackend, AutoBackend, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr chosen = AcquireByName(preferredBackend);
                if (chosen != IntPtr.Zero)
                {
                    Adopt(chosen);
                    return;
                }

                Log.Warn(LogTag, $"preferred backend '{preferredBackend}' unavailable; falling back to automatic");
            }

            IntPtr best = PrismInterop.prism_registry_acquire_best(_ctx);
            if (best != IntPtr.Zero)
                Adopt(best);
        }

        /// <summary>Adopts a backend as the normal channel, gated on it actually supporting speech.</summary>
        private static void Adopt(IntPtr backend)
        {
            ulong features = PrismInterop.prism_backend_get_features(backend);
            if ((features & PrismInterop.FeatureSupportsSpeak) == 0)
            {
                Log.Warn(LogTag, $"backend '{PrismInterop.FromUtf8(PrismInterop.prism_backend_name(backend))}' " +
                                 "does not support speech; ignoring it");
                return;
            }

            _normal = backend;
            _normalName = PrismInterop.FromUtf8(PrismInterop.prism_backend_name(backend)) ?? "Unknown";
            _available = true;
        }

        /// <summary>
        /// Acquires a backend by its registry name and initialises it. Unlike acquire_best,
        /// an explicit acquire hands back an uninitialised instance, so initialize() is ours
        /// to call; ALREADY_INITIALIZED means we got a cached one and is not a failure.
        /// </summary>
        private static IntPtr AcquireByName(string name)
        {
            ulong id = FindBackendId(name);
            if (id == 0 || !PrismInterop.prism_registry_exists(_ctx, id))
                return IntPtr.Zero;

            IntPtr backend = PrismInterop.prism_registry_acquire(_ctx, id);
            if (backend == IntPtr.Zero)
                return IntPtr.Zero;

            int rc = PrismInterop.prism_backend_initialize(backend);
            if (rc != PrismInterop.PRISM_OK && rc != PrismInterop.PRISM_ERROR_ALREADY_INITIALIZED)
            {
                Log.Warn(LogTag, $"prism_backend_initialize('{name}') failed: {PrismInterop.ErrorText(rc)}");
                return IntPtr.Zero;
            }

            return backend;
        }

        /// <summary>
        /// Acquires SAPI for the urgent channel and applies rate and volume. Existence is
        /// checked first so we never hand an unregistered id to acquire.
        /// </summary>
        private static void AcquireSapi()
        {
            _sapi = IntPtr.Zero;
            _sapiReady = false;

            if (!_available)
                return;

            if (!PrismInterop.prism_registry_exists(_ctx, PrismInterop.BackendSapi))
            {
                Log.Msg(LogTag, "SAPI backend not in registry; urgent speech will use the normal backend");
                return;
            }

            IntPtr backend = PrismInterop.prism_registry_acquire(_ctx, PrismInterop.BackendSapi);
            if (backend == IntPtr.Zero)
            {
                Log.Msg(LogTag, "prism_registry_acquire(SAPI) returned NULL; urgent speech will use the normal backend");
                return;
            }

            int rc = PrismInterop.prism_backend_initialize(backend);
            if (rc != PrismInterop.PRISM_OK && rc != PrismInterop.PRISM_ERROR_ALREADY_INITIALIZED)
            {
                Log.Msg(LogTag, $"prism_backend_initialize(SAPI) failed: {PrismInterop.ErrorText(rc)}");
                return;
            }

            _sapi = backend;
            _sapiReady = true;

            ApplySapiRate();
            ApplySapiVolume();
        }

        private static void ApplySapiRate()
        {
            if (!_sapiReady || !_canSetRate)
                return;

            try
            {
                int rc = PrismInterop.prism_backend_set_rate(_sapi, SapiUrgentRate);
                if (rc != PrismInterop.PRISM_OK)
                    Log.Msg(LogTag, $"prism_backend_set_rate(SAPI, {SapiUrgentRate}) — {PrismInterop.ErrorText(rc)}; staying at the default rate");
            }
            catch (EntryPointNotFoundException)
            {
                _canSetRate = false;
                Log.Msg(LogTag, "prism_backend_set_rate not exported by this prism.dll; staying at the default rate");
            }
        }

        private static void ApplySapiVolume()
        {
            if (!_sapiReady || !_canSetVolume)
                return;

            try
            {
                float volume = _urgentVolumePercent / 100f;
                int rc = PrismInterop.prism_backend_set_volume(_sapi, volume);
                if (rc != PrismInterop.PRISM_OK)
                    Log.Msg(LogTag, $"prism_backend_set_volume(SAPI, {volume:0.00}) — {PrismInterop.ErrorText(rc)}; staying at the default volume");
            }
            catch (EntryPointNotFoundException)
            {
                _canSetVolume = false;
                Log.Msg(LogTag, "prism_backend_set_volume not exported by this prism.dll; staying at the default volume");
            }
        }

        /// <summary>
        /// Releases our references. Deliberately does not call prism_shutdown: Prism's backends
        /// hold COM and RPC objects, and tearing those down while the process is already exiting
        /// risks loader-lock and COM-uninitialise ordering problems. Process exit reclaims them.
        /// </summary>
        public static void Shutdown()
        {
            lock (Gate)
            {
                _available = false;
                _sapiReady = false;
                _normal = IntPtr.Zero;
                _sapi = IntPtr.Zero;
                _initialized = false;
            }
        }

        /// <summary>
        /// Speaks through the normal channel. <paramref name="interrupt"/> carries the
        /// announcement priority decision straight through to the screen reader: Critical and
        /// Immediate announcements interrupt, Normal and High queue.
        /// </summary>
        public static void Speak(string text, bool interrupt = false)
        {
            if (!_available || string.IsNullOrEmpty(text))
                return;

            byte[] utf8 = PrismInterop.ToUtf8(text);
            if (utf8 == null)
                return;

            lock (Gate)
            {
                if (!_available)
                    return;

                try
                {
                    int rc = PrismInterop.prism_backend_speak(_normal, utf8, interrupt);
                    if (rc != PrismInterop.PRISM_OK)
                        Log.Warn(LogTag, $"speak failed, dropping utterance: {PrismInterop.ErrorText(rc)}");
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"speak threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Speaks through the SAPI urgent channel, always interrupting. Falls back to the normal
        /// channel only when SAPI never came up — never after a failed SAPI dispatch, because
        /// SAPI speaks asynchronously and may already be mid-utterance when it reports an error,
        /// which would produce the same text twice in two voices.
        /// </summary>
        public static void SpeakUrgent(string text)
        {
            if (!_available || string.IsNullOrEmpty(text))
                return;

            if (!_sapiReady)
            {
                Speak(text, true);
                return;
            }

            byte[] utf8 = PrismInterop.ToUtf8(text);
            if (utf8 == null)
                return;

            lock (Gate)
            {
                if (!_sapiReady)
                    return;

                try
                {
                    int rc = PrismInterop.prism_backend_speak(_sapi, utf8, true);
                    if (rc != PrismInterop.PRISM_OK)
                        Log.Warn(LogTag, $"urgent speak failed, dropping utterance (no fallback, would double-speak): {PrismInterop.ErrorText(rc)}");
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"urgent speak threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>Stops speech on both channels.</summary>
        public static void Silence()
        {
            if (!_available)
                return;

            lock (Gate)
            {
                if (!_available)
                    return;

                try
                {
                    if (_normal != IntPtr.Zero)
                        PrismInterop.prism_backend_stop(_normal);
                    if (_sapi != IntPtr.Zero)
                        PrismInterop.prism_backend_stop(_sapi);
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"silence threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>True while the normal channel is speaking. False when the backend cannot report it.</summary>
        public static bool IsSpeaking()
        {
            if (!_available)
                return false;

            lock (Gate)
            {
                if (!_available)
                    return false;

                try
                {
                    ulong features = PrismInterop.prism_backend_get_features(_normal);
                    if ((features & PrismInterop.FeatureSupportsIsSpeaking) == 0)
                        return false;

                    byte speaking;
                    return PrismInterop.prism_backend_is_speaking(_normal, out speaking) == PrismInterop.PRISM_OK
                           && speaking != 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Name of the backend currently carrying announcements, e.g. "NVDA" or "SAPI".</summary>
        public static string GetActiveScreenReader()
        {
            return _available ? _normalName : "None";
        }

        /// <summary>
        /// Registry names of every backend Prism has compiled in, whether or not the matching
        /// reader is running. Feeds the backend picker in the settings menu.
        /// </summary>
        public static string[] GetAvailableBackends()
        {
            lock (Gate)
            {
                if (_ctx == IntPtr.Zero)
                    return new string[0];

                try
                {
                    var names = new List<string>();
                    long count = PrismInterop.prism_registry_count(_ctx).ToInt64();
                    for (long i = 0; i < count; i++)
                    {
                        ulong id = PrismInterop.prism_registry_id_at(_ctx, new IntPtr(i));
                        if (id == 0)
                            continue;

                        string name = PrismInterop.FromUtf8(PrismInterop.prism_registry_name(_ctx, id));
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                    return names.ToArray();
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"could not enumerate backends: {ex.Message}");
                    return new string[0];
                }
            }
        }

        /// <summary>
        /// Switches the normal channel to a named backend, or back to automatic selection.
        /// Returns true when the request was honoured exactly — a named backend that was gone
        /// leaves acquire_best's choice speaking and returns false, so the caller can say so.
        /// On outright failure the previous backend keeps running: the user is never left
        /// without speech mid-session.
        /// </summary>
        public static bool SelectBackend(string backendName)
        {
            lock (Gate)
            {
                if (_ctx == IntPtr.Zero)
                    return false;

                IntPtr previous = _normal;
                string previousName = _normalName;
                bool previousAvailable = _available;

                try
                {
                    AcquireNormal(backendName);
                    if (_available)
                    {
                        Log.Msg(LogTag, $"normal backend switched to {_normalName}");
                        bool wantedAuto = string.IsNullOrEmpty(backendName) ||
                                          string.Equals(backendName, AutoBackend, StringComparison.OrdinalIgnoreCase);
                        return wantedAuto || string.Equals(backendName, _normalName, StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"backend switch to '{backendName}' threw {ex.GetType().Name}: {ex.Message}");
                }

                _normal = previous;
                _normalName = previousName;
                _available = previousAvailable;
                Log.Warn(LogTag, $"backend switch to '{backendName}' failed; staying on {previousName}");
                return false;
            }
        }

        /// <summary>Sets the urgent channel volume (0-100) and pushes it to the live SAPI backend.</summary>
        public static void SetUrgentVolumePercent(int percent)
        {
            lock (Gate)
            {
                _urgentVolumePercent = Clamp(percent, 0, 100);
                ApplySapiVolume();
            }
        }

        private static ulong FindBackendId(string name)
        {
            long count = PrismInterop.prism_registry_count(_ctx).ToInt64();
            for (long i = 0; i < count; i++)
            {
                ulong id = PrismInterop.prism_registry_id_at(_ctx, new IntPtr(i));
                if (id == 0)
                    continue;

                string candidate = PrismInterop.FromUtf8(PrismInterop.prism_registry_name(_ctx, id));
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    return id;
            }
            return 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
