using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AccessibleArena.Core.Speech
{
    /// <summary>
    /// Detects Wine/Proton and converts paths between the Windows and Linux views of the file
    /// system. Every call is safe on real Windows: the Wine-only exports simply are not found,
    /// <see cref="IsWine"/> is false, and the Linux speech path is never taken.
    /// </summary>
    internal static class WineInterop
    {
        private static bool? _isWine;
        private static string _version;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcessHeap();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool HeapFree(IntPtr heap, uint flags, IntPtr mem);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr WineGetVersionFn();

        // char* wine_get_unix_file_name(const WCHAR*), result on the process heap.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr WineGetUnixFileNameFn([MarshalAs(UnmanagedType.LPWStr)] string dosPath);

        // WCHAR* wine_get_dos_file_name(const char*), result on the process heap.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr WineGetDosFileNameFn(byte[] unixPathUtf8);

        /// <summary>
        /// True when the game runs under Wine or Proton. <c>AA_FORCE_LINUX_SPEECH=1</c> forces
        /// it on and <c>=0</c> forces it off, for testing and for a misdetected setup.
        /// </summary>
        internal static bool IsWine
        {
            get
            {
                if (_isWine.HasValue)
                    return _isWine.Value;

                string force = SafeEnv("AA_FORCE_LINUX_SPEECH");
                if (force == "1")
                    _isWine = true;
                else if (force == "0")
                    _isWine = false;
                else
                    _isWine = DetectWine();

                return _isWine.Value;
            }
        }

        /// <summary>Wine version string, e.g. "9.0", or null when unknown.</summary>
        internal static string Version
        {
            get
            {
                bool unused = IsWine;
                return _version;
            }
        }

        private static bool DetectWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandleW("ntdll.dll");
                if (ntdll == IntPtr.Zero)
                    return false;

                IntPtr fn = GetProcAddress(ntdll, "wine_get_version");
                if (fn == IntPtr.Zero)
                    return false;

                try
                {
                    var getVersion = (WineGetVersionFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(WineGetVersionFn));
                    _version = Marshal.PtrToStringAnsi(getVersion());
                }
                catch (Exception)
                {
                    // Detection does not depend on the version string.
                }
                return true;
            }
            catch (Exception)
            {
                // Not Windows at all (unit tests on Linux/macOS) or kernel32 unavailable.
                return false;
            }
        }

        /// <summary>
        /// Linux path for a Windows path (e.g. <c>Z:\home\me</c> → <c>/home/me</c>), or null.
        /// </summary>
        internal static string ToUnixPath(string windowsPath)
        {
            if (string.IsNullOrEmpty(windowsPath) || !IsWine)
                return null;

            try
            {
                IntPtr fn = GetKernel32Export("wine_get_unix_file_name");
                if (fn != IntPtr.Zero)
                {
                    var convert = (WineGetUnixFileNameFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(WineGetUnixFileNameFn));
                    IntPtr result = convert(windowsPath);
                    if (result != IntPtr.Zero)
                    {
                        try
                        {
                            return PrismInterop.FromUtf8(result);
                        }
                        finally
                        {
                            HeapFree(GetProcessHeap(), 0, result);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the Z: convention.
            }

            if (windowsPath.Length > 2 && (windowsPath[0] == 'Z' || windowsPath[0] == 'z') && windowsPath[1] == ':')
                return windowsPath.Substring(2).Replace('\\', '/');

            return null;
        }

        /// <summary>
        /// Windows path for a Linux path (e.g. <c>/usr/bin/python3</c> → <c>Z:\usr\bin\python3</c>).
        /// Wine maps the whole Linux file system on Z: by default; the conversion asks Wine
        /// first so a prefix without Z: still resolves.
        /// </summary>
        internal static string ToWindowsPath(string unixPath)
        {
            if (string.IsNullOrEmpty(unixPath))
                return null;

            if (IsWine)
            {
                try
                {
                    IntPtr fn = GetKernel32Export("wine_get_dos_file_name");
                    if (fn != IntPtr.Zero)
                    {
                        var convert = (WineGetDosFileNameFn)Marshal.GetDelegateForFunctionPointer(fn, typeof(WineGetDosFileNameFn));
                        IntPtr result = convert(PrismInterop.ToUtf8(unixPath));
                        if (result != IntPtr.Zero)
                        {
                            try
                            {
                                return Marshal.PtrToStringUni(result);
                            }
                            finally
                            {
                                HeapFree(GetProcessHeap(), 0, result);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fall through to the Z: convention.
                }
            }

            return "Z:" + unixPath.Replace('/', '\\');
        }

        /// <summary>
        /// First of <paramref name="unixCandidates"/> that exists, as a Windows path ready for
        /// Process.Start, or null. Wine starts native Linux programs given such a path.
        /// </summary>
        internal static string FindLinuxProgram(params string[] unixCandidates)
        {
            foreach (string candidate in unixCandidates)
            {
                try
                {
                    string windowsPath = ToWindowsPath(candidate);
                    if (windowsPath != null && File.Exists(windowsPath))
                        return windowsPath;
                }
                catch (Exception)
                {
                    // Unreadable path; try the next one.
                }
            }
            return null;
        }

        internal static string SafeEnv(string name)
        {
            try
            {
                return Environment.GetEnvironmentVariable(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IntPtr GetKernel32Export(string name)
        {
            IntPtr kernel32 = GetModuleHandleW("kernel32.dll");
            return kernel32 == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(kernel32, name);
        }
    }
}
