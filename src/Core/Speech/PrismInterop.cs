using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AccessibleArena.Core.Speech
{
    /// <summary>
    /// Raw P/Invoke surface for Prism's C ABI (prism.h, v0.16.5) — the screen-reader
    /// abstraction that replaced Tolk. See <see cref="AccessibleArena.ScreenReaderOutput"/>
    /// for the policy layer; this file is signatures and constants only.
    ///
    /// Marshalling notes, all of them load-bearing:
    /// <list type="bullet">
    /// <item>Every export is <c>__cdecl</c> with undecorated names (PRISM_CALL on Windows).</item>
    /// <item>C <c>bool</c> is one byte, not a 4-byte Win32 BOOL — every bool argument and
    /// return is <c>UnmanagedType.I1</c>. The default marshalling would write four bytes.</item>
    /// <item>Text is UTF-8 and Prism strict-validates it (PRISM_ERROR_INVALID_UTF8). Strings
    /// go over as NUL-terminated <c>byte[]</c> built by <see cref="ToUtf8"/> rather than as
    /// <c>string</c>, because the default string marshalling on .NET Framework is ANSI and
    /// would silently mangle any card name with an accent.</item>
    /// <item><c>prism_config_init</c> returns a one-byte struct by value. The Windows x64 ABI
    /// returns that in RAX, so it is declared as returning <c>byte</c> — this sidesteps
    /// struct-return marshalling under Unity's Mono entirely.</item>
    /// <item><c>size_t</c> parameters are <c>IntPtr</c> so the same declarations stay correct
    /// if the mod is ever built for another word size.</item>
    /// </list>
    /// </summary>
    internal static class PrismInterop
    {
        internal const string Dll = "prism.dll";

        // --- PrismError (prism.h) — only the values we branch on. ---
        internal const int PRISM_OK = 0;
        /// <summary>Returned when a backend was already brought up by an earlier acquire. Not a failure.</summary>
        internal const int PRISM_ERROR_ALREADY_INITIALIZED = 15;

        // --- Backend ids (prism.h PRISM_BACKEND_*). ---
        internal const ulong BackendSapi = 0x1D6DF72422CEEE66UL;

        // --- PrismBackendFeature bits we query before calling an optional entry point. ---
        internal const ulong FeatureSupportsSpeak = 1UL << 2;
        internal const ulong FeatureSupportsIsSpeaking = 1UL << 6;
        internal const ulong FeatureSupportsStop = 1UL << 7;
        internal const ulong FeatureSupportsSetVolume = 1UL << 10;
        internal const ulong FeatureSupportsSetRate = 1UL << 12;
        internal const ulong FeatureSupportsCountVoices = 1UL << 17;
        internal const ulong FeatureSupportsSetVoice = 1UL << 21;

        /// <summary>
        /// Mirrors prism.h's <c>PrismConfig</c>: a single version byte. Filled from
        /// <see cref="prism_config_init"/> so the shipped DLL decides its own version
        /// rather than us hard-coding PRISM_CONFIG_VERSION.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = 1)]
        internal struct PrismConfig
        {
            internal byte Version;
        }

        // --- Context lifecycle ---

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte prism_config_init();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_init(ref PrismConfig cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void prism_shutdown(IntPtr ctx);

        // --- Registry ---

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_count(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong prism_registry_id_at(IntPtr ctx, IntPtr index);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_name(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_registry_priority(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool prism_registry_exists(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_acquire(IntPtr ctx, ulong id);

        /// <summary>
        /// Walks every registered backend in priority order (NVDA, JAWS, ZDSR, PC-Talker,
        /// OneCore, UIA, SAPI, ...) and returns the first whose initialize() succeeds —
        /// already initialised. In the patched build we ship, a backend whose vendor DLL
        /// faults during initialize() is skipped rather than taking the process down.
        /// </summary>
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_acquire_best(IntPtr ctx);

        // --- Backend ---

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong prism_backend_get_features(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_initialize(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_speak(IntPtr backend, byte[] utf8Text,
                                                       [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_stop(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_is_speaking(IntPtr backend, out byte outSpeaking);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_set_volume(IntPtr backend, float volume);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_set_rate(IntPtr backend, float rate);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_count_voices(IntPtr backend, out IntPtr outCount);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_get_voice_name(IntPtr backend, IntPtr voiceId, out IntPtr outName);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_set_voice(IntPtr backend, IntPtr voiceId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_error_string(int error);

        // --- Windows loader (explicit preload so a missing DLL is one clear log line) ---

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryW(string path);

        // --- Helpers ---

        /// <summary>
        /// Encodes managed text as NUL-terminated UTF-8. Prism rejects anything that is not
        /// valid UTF-8 and drops the whole utterance, so this is the only path text may take.
        /// </summary>
        internal static byte[] ToUtf8(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            int byteCount = Encoding.UTF8.GetByteCount(text);
            var buffer = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
            buffer[byteCount] = 0;
            return buffer;
        }

        /// <summary>
        /// Reads a NUL-terminated UTF-8 string Prism owns. Marshal.PtrToStringAnsi would
        /// decode with the system ANSI codepage instead, so it is not used here.
        /// </summary>
        internal static string FromUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0)
                length++;

            if (length == 0)
                return string.Empty;

            var buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>Human-readable form of a PrismError for the log, with the raw code kept.</summary>
        internal static string ErrorText(int error)
        {
            try
            {
                string text = FromUtf8(prism_error_string(error));
                return string.IsNullOrEmpty(text) ? $"rc={error}" : $"{text} (rc={error})";
            }
            catch (Exception)
            {
                // prism_error_string is optional in older builds; the code alone still tells us enough.
                return $"rc={error}";
            }
        }
    }
}
