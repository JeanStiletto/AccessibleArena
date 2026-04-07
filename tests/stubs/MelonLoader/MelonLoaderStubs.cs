// Stub for MelonLoader.dll — no-op implementations for test environments.
// The assembly name is "MelonLoader" so the CLR resolves this instead of the real DLL.
namespace MelonLoader
{
    public static class MelonLogger
    {
        public static void Msg(string message) { }
        public static void Msg(object obj) { }
        public static void Warning(string message) { }
        public static void Warning(object obj) { }
        public static void Error(string message) { }
        public static void Error(object obj) { }
        public static void BigError(string message) { }
    }
}
