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
        private static bool _normalSupportsOutput;   // combined speak+braille call (Tolk_Output equivalent)
        private static bool _normalSupportsBraille;  // braille-only call
        private static int _urgentVolumePercent = 100;
        private static string _preferredBackend = AutoBackend;

        /// <summary>
        /// Throttle for the re-acquire that <see cref="Speak"/> runs when the backend reports
        /// itself gone. A walk costs an RPC round trip and a COM class lookup, and announcements
        /// arrive in bursts, so a reader that stays down must not pay for one walk per utterance.
        /// </summary>
        private static readonly TimeSpan ReacquireInterval = TimeSpan.FromSeconds(5);
        private static DateTime _lastReacquire = DateTime.MinValue;

        // Optional entry points: older prism.dll builds may not export them, and a backend
        // without the matching SUPPORTS_* feature bit fails at call time. Each is probed once
        // and latched off, so a missing export costs one exception rather than one per call.
        private static bool _canSetVolume = true;
        private static bool _canSetRate = true;
        private static bool _canOutput = true;
        private static bool _canBraille = true;

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
                _preferredBackend = string.IsNullOrEmpty(preferredBackend) ? AutoBackend : preferredBackend;

                try
                {
                    PreloadLibrary();

                    // Reuse the context across a Shutdown/Initialize cycle — prism_shutdown is
                    // deliberately never called (see Shutdown), so a second init would otherwise
                    // strand the first context.
                    if (_ctx == IntPtr.Zero)
                        _ctx = PrismInterop.prism_init(IntPtr.Zero);

                    if (_ctx == IntPtr.Zero)
                    {
                        Log.Warn(LogTag, "prism_init returned NULL; running silent");
                        return false;
                    }

                    AcquireNormal(preferredBackend);
                    AcquireSapi();

                    if (_available)
                    {
                        Log.Msg(LogTag, $"ready — normal backend = {_normalName} ({BrailleMode()}), urgent backend = " +
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
        /// Acquires the normal channel. A named preference is tried first, then acquire_best —
        /// Prism owns that priority walk and (in the patched build we ship) skips a backend whose
        /// vendor DLL faults during initialize(). Whatever comes back still has to pass
        /// <see cref="Adopt"/>'s liveness gate, and <see cref="AcquireFirstLive"/> takes over when
        /// it does not.
        /// </summary>
        private static void AcquireNormal(string preferredBackend)
        {
            _normal = IntPtr.Zero;
            _available = false;

            if (!string.IsNullOrEmpty(preferredBackend) &&
                !string.Equals(preferredBackend, AutoBackend, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr chosen = AcquireByName(preferredBackend);
                if (chosen != IntPtr.Zero && Adopt(chosen))
                    return;

                Release(chosen);
                Log.Warn(LogTag, $"preferred backend '{preferredBackend}' unavailable; falling back to automatic");
            }

            IntPtr best = PrismInterop.prism_registry_acquire_best(_ctx);
            if (best != IntPtr.Zero && Adopt(best))
                return;

            if (best != IntPtr.Zero)
            {
                Log.Msg(LogTag, $"acquire_best chose '{NameOf(best)}' but it is not live; walking the registry");
                Release(best);
            }

            AcquireFirstLive();
        }

        /// <summary>
        /// Walks the registry and adopts the first backend that is live and initialises. Registry
        /// order is descending priority (Prism inserts by priority), so this reproduces
        /// acquire_best's ordering — which we cannot simply call again, because it caches its first
        /// pick and would hand back the same dead backend.
        ///
        /// Liveness is checked before initialize(), never after, so a reader that is not running
        /// never gets its vendor client library loaded. That is what keeps this walk as safe as
        /// Prism's own SEH-guarded one despite running unguarded from managed code.
        /// </summary>
        private static void AcquireFirstLive()
        {
            const ulong required = PrismInterop.FeatureSupportsSpeak | PrismInterop.FeatureIsSupportedAtRuntime;

            long count = PrismInterop.prism_registry_count(_ctx).ToInt64();
            for (long i = 0; i < count; i++)
            {
                ulong id = PrismInterop.prism_registry_id_at(_ctx, new IntPtr(i));
                if (id == 0)
                    continue;

                IntPtr backend = PrismInterop.prism_registry_acquire(_ctx, id);
                if (backend == IntPtr.Zero)
                    continue;

                if ((PrismInterop.prism_backend_get_features(backend) & required) != required)
                {
                    Release(backend);
                    continue;
                }

                int rc = PrismInterop.prism_backend_initialize(backend);
                if (rc != PrismInterop.PRISM_OK && rc != PrismInterop.PRISM_ERROR_ALREADY_INITIALIZED)
                {
                    Log.Msg(LogTag, $"backend '{NameOf(backend)}' reported itself live but " +
                                    $"initialize failed: {PrismInterop.ErrorText(rc)}; trying the next one");
                    Release(backend);
                    continue;
                }

                if (Adopt(backend))
                    return;

                Release(backend);
            }

            Log.Warn(LogTag, "no live speech backend found; running silent");
        }

        /// <summary>
        /// Adopts a backend as the normal channel. It has to support speech and report itself
        /// live — a successful initialize() alone is not enough: Prism 0.16.5's NVDA backend
        /// returns success when NVDA is not running, because its testIfRunning check sits behind
        /// an RPC interface query that itself fails when there is no server to query. NVDA holds
        /// the highest priority, so unchecked it wins acquire_best on every machine and then
        /// drops every utterance with BACKEND_NOT_AVAILABLE — total silence for anyone not
        /// running NVDA. IS_SUPPORTED_AT_RUNTIME is the check initialize() should have made.
        /// </summary>
        private static bool Adopt(IntPtr backend)
        {
            ulong features = PrismInterop.prism_backend_get_features(backend);

            if ((features & PrismInterop.FeatureSupportsSpeak) == 0)
            {
                Log.Warn(LogTag, $"backend '{NameOf(backend)}' does not support speech; ignoring it");
                return false;
            }

            if ((features & PrismInterop.FeatureIsSupportedAtRuntime) == 0)
            {
                Log.Msg(LogTag, $"backend '{NameOf(backend)}' initialised but reports it is not " +
                                "running; ignoring it");
                return false;
            }

            _normal = backend;
            _normalName = NameOf(backend);
            _normalSupportsOutput = (features & PrismInterop.FeatureSupportsOutput) != 0;
            _normalSupportsBraille = (features & PrismInterop.FeatureSupportsBraille) != 0;
            _available = true;
            return true;
        }

        /// <summary>
        /// How the adopted backend reaches a braille display, for the log — the line a braille
        /// user's report gets checked against. Keyed on SUPPORTS_BRAILLE, not SUPPORTS_OUTPUT:
        /// every backend advertises output() (on the speech-only ones its braille half is a
        /// no-op), so only the braille bit says whether a display can actually be reached.
        /// </summary>
        private static string BrailleMode()
        {
            if (!_normalSupportsBraille)
                return "no braille";
            return _normalSupportsOutput && _canOutput ? "braille via output()" : "braille supported";
        }

        /// <summary>Registry name of a backend, for the log.</summary>
        private static string NameOf(IntPtr backend)
        {
            return PrismInterop.FromUtf8(PrismInterop.prism_backend_name(backend)) ?? "Unknown";
        }

        /// <summary>
        /// Hands back a backend handle we decided against. Only ever called on a handle that was
        /// not adopted, so it can never release the channel that is speaking. Failure is ignored:
        /// the handle is already unreachable, and a leak must not cost us a backend.
        /// </summary>
        private static void Release(IntPtr backend)
        {
            if (backend == IntPtr.Zero)
                return;

            try
            {
                PrismInterop.prism_backend_free(backend);
            }
            catch (Exception)
            {
                // Older prism.dll builds may not export it; nothing here is worth a log line.
            }
        }

        /// <summary>
        /// Re-runs backend selection after the live backend reported itself gone, so that closing
        /// one reader and opening another mid-session recovers speech instead of ending it. Rate
        /// limited by <see cref="ReacquireInterval"/>, and the previous backend is put back on
        /// failure — a failed re-selection must never be what silences a working session.
        /// Caller holds <see cref="Gate"/>. Returns true when a live backend is now selected.
        /// </summary>
        private static bool TryReacquireNormal()
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastReacquire < ReacquireInterval)
                return false;
            _lastReacquire = now;

            IntPtr previous = _normal;
            string previousName = _normalName;
            bool previousAvailable = _available;

            try
            {
                AcquireNormal(_preferredBackend);
                if (_available)
                {
                    Log.Msg(LogTag, $"'{previousName}' went away; normal backend is now {_normalName} ({BrailleMode()})");

                    // Every acquire allocates a fresh handle, so the replacement is never the
                    // handle we are about to release — checked anyway, because releasing the
                    // channel that is about to speak would be a use-after-free.
                    if (previous != _normal)
                        Release(previous);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"re-acquiring after a lost backend threw {ex.GetType().Name}: {ex.Message}");
            }

            _normal = previous;
            _normalName = previousName;
            _available = previousAvailable;
            return false;
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
                    int rc = DispatchNormal(utf8, interrupt);

                    // The reader went away since we adopted it — it was closed, restarted, or
                    // never really there. Re-select and say this one on the new backend rather
                    // than losing it.
                    if (rc == PrismInterop.PRISM_ERROR_BACKEND_NOT_AVAILABLE && TryReacquireNormal())
                        rc = DispatchNormal(utf8, interrupt);

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
        /// Sends one announcement to the normal backend on every channel it offers. Tolk's
        /// Tolk_Output spoke and brailled in one call; Prism splits those, so a backend
        /// advertising SUPPORTS_OUTPUT gets prism_backend_output (the direct equivalent) and
        /// the rest get speak plus, where supported, a braille flash message. Caller holds
        /// <see cref="Gate"/>.
        /// </summary>
        private static int DispatchNormal(byte[] utf8, bool interrupt)
        {
            if (_normalSupportsOutput && _canOutput)
            {
                try
                {
                    int rc = PrismInterop.prism_backend_output(_normal, utf8, interrupt);
                    if (rc != PrismInterop.PRISM_ERROR_NOT_IMPLEMENTED)
                        return rc;

                    // The feature bit promised output() but the call disagreed — an older vendor
                    // client DLL can do that. Take speak (+ braille) from here on.
                    _normalSupportsOutput = false;
                    Log.Msg(LogTag, $"'{_normalName}' advertises output() but reports NotImplemented; " +
                                    $"switching to speak ({BrailleMode()})");
                }
                catch (EntryPointNotFoundException)
                {
                    _canOutput = false;
                    Log.Msg(LogTag, "prism_backend_output not exported by this prism.dll; using speak" +
                                    (_normalSupportsBraille ? " + braille" : ""));
                }
            }

            int rcSpeak = PrismInterop.prism_backend_speak(_normal, utf8, interrupt);
            BrailleNormal(utf8);
            return rcSpeak;
        }

        /// <summary>
        /// Mirrors text to the braille display through the normal backend, when it has one.
        /// Quiet by design: braille is supplementary on this path, so a failure must never cost
        /// speech or spam the log — only a NotImplemented or missing export is noted, once, when
        /// latching the capability off. Caller holds <see cref="Gate"/>.
        /// </summary>
        private static void BrailleNormal(byte[] utf8)
        {
            if (!_normalSupportsBraille || !_canBraille)
                return;

            try
            {
                int rc = PrismInterop.prism_backend_braille(_normal, utf8);
                if (rc == PrismInterop.PRISM_ERROR_NOT_IMPLEMENTED)
                {
                    // Advertised but not really there — e.g. a ZDSRAPI predating its Braille export.
                    _normalSupportsBraille = false;
                    Log.Msg(LogTag, $"'{_normalName}' advertises braille but reports NotImplemented; " +
                                    "braille off for this backend");
                }
            }
            catch (EntryPointNotFoundException)
            {
                _canBraille = false;
                Log.Msg(LogTag, "prism_backend_braille not exported by this prism.dll; braille mirroring disabled");
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

                    // SAPI has no braille display, so mirror the alert through the normal
                    // backend's braille channel — braille() makes no sound, so unlike a speech
                    // fallback this cannot say the text twice.
                    if (_available)
                        BrailleNormal(utf8);
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
                        _preferredBackend = string.IsNullOrEmpty(backendName) ? AutoBackend : backendName;
                        Log.Msg(LogTag, $"normal backend switched to {_normalName} ({BrailleMode()})");
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
