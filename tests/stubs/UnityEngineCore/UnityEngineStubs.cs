// Stub for UnityEngine.CoreModule.dll — minimal types needed by testable code.
// The assembly name is "UnityEngine.CoreModule" so the CLR resolves this instead of the real DLL.
namespace UnityEngine
{
    /// <summary>Unity KeyCode enum — integer values match the real Unity enum exactly.</summary>
    public enum KeyCode
    {
        None = 0,
        Backspace = 8,
        Tab = 9,
        Return = 13,
        Escape = 27,
        Space = 32,
        // Digits
        Alpha0 = 48, Alpha1 = 49, Alpha2 = 50, Alpha3 = 51, Alpha4 = 52,
        Alpha5 = 53, Alpha6 = 54, Alpha7 = 55, Alpha8 = 56, Alpha9 = 57,
        // Alphabet
        A = 97,  B = 98,  C = 99,  D = 100, E = 101, F = 102, G = 103,
        H = 104, I = 105, J = 106, K = 107, L = 108, M = 109, N = 110,
        O = 111, P = 112, Q = 113, R = 114, S = 115, T = 116, U = 117,
        V = 118, W = 119, X = 120, Y = 121, Z = 122,
        // Function keys
        F1  = 282, F2  = 283, F3  = 284, F4  = 285, F5  = 286, F6  = 287,
        F7  = 288, F8  = 289, F9  = 290, F10 = 291, F11 = 292, F12 = 293,
        // Modifiers
        RightShift   = 303,
        LeftShift    = 304,
        RightControl = 305,
        LeftControl  = 306,
        RightAlt     = 307,
        LeftAlt      = 308,
    }

    /// <summary>
    /// Stub for UnityEngine.Time. In tests, set <see cref="time"/> directly to control timing.
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// Writable in tests (unlike the real Unity property which is engine-driven).
        /// </summary>
        public static float time;
    }

    /// <summary>Minimal stub so any code that references GameObject compiles.</summary>
    public class GameObject { }

    /// <summary>Minimal stub so any code that references MonoBehaviour compiles.</summary>
    public class MonoBehaviour { }

    /// <summary>Minimal stub so any code that references Component compiles.</summary>
    public class Component { }
}
