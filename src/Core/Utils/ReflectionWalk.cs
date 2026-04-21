using System;
using System.Reflection;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Companion to <see cref="ReflectionCache{THandles}"/>. <c>Type.GetField(name, NonPublic | Instance)</c>
    /// does NOT inherit — private fields declared on a base class return null without a walk. These helpers
    /// walk <see cref="Type.BaseType"/> until a match is found or the chain ends.
    /// </summary>
    public static class ReflectionWalk
    {
        public static FieldInfo FindField(Type type, string name, BindingFlags flags)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, flags);
                if (f != null) return f;
            }
            return null;
        }

        public static PropertyInfo FindProperty(Type type, string name, BindingFlags flags)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, flags);
                if (p != null) return p;
            }
            return null;
        }

        public static MethodInfo FindMethod(Type type, string name, BindingFlags flags)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, flags);
                if (m != null) return m;
            }
            return null;
        }
    }
}
