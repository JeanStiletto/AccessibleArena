using System;
using System.Runtime.InteropServices;

namespace AccessibleArena
{
    public static class ScreenReaderOutput
    {
        private static bool _initialized;
        private static bool _available;

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_IsLoaded();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_HasSpeech();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Output(
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            [MarshalAs(UnmanagedType.Bool)] bool interrupt);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Speak(
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            [MarshalAs(UnmanagedType.Bool)] bool interrupt);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Silence();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Tolk_DetectScreenReader();

        public static bool Initialize()
        {
            if (_initialized)
                return _available;

            try
            {
                Tolk_Load();
                _available = Tolk_IsLoaded() && Tolk_HasSpeech();
                _initialized = true;
            }
            catch (DllNotFoundException)
            {
                _available = false;
                _initialized = true;
            }

            return _available;
        }

        public static void Shutdown()
        {
            if (_initialized && _available)
            {
                Tolk_Unload();
            }
            _initialized = false;
            _available = false;
        }

        public static void Speak(string text, bool interrupt = false)
        {
            if (!_available)
                return;

            Tolk_Output(text, interrupt);
        }

        public static void SpeakInterrupt(string text)
        {
            Speak(text, true);
        }

        public static void Silence()
        {
            if (_available)
            {
                Tolk_Silence();
            }
        }

        public static string GetActiveScreenReader()
        {
            if (!_available)
                return "None";

            IntPtr ptr = Tolk_DetectScreenReader();
            return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) : "Unknown";
        }

        public static bool IsAvailable => _available;
    }
}
