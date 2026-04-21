using System;
using System.Collections.Generic;
using System.Reflection;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Generic reflection-handle cache. Each call site declares a small <typeparamref name="THandles"/>
    /// class with public fields (<see cref="FieldInfo"/> / <see cref="PropertyInfo"/> / <see cref="MethodInfo"/>),
    /// supplies a builder lambda and a validator predicate, and reads cached handles through
    /// <see cref="Handles"/>.
    ///
    /// Log shape (preserved from the hand-rolled init methods this helper replaces):
    ///   success: <c>[Tag] Subject reflection initialized</c>
    ///   validator failure: <c>[Tag] Could not resolve required handles for Subject: Name1, Name2</c>
    ///   builder exception: <c>[Tag] Failed to initialize Subject reflection: &lt;ex.Message&gt;</c>
    /// </summary>
    public sealed class ReflectionCache<THandles> where THandles : class, new()
    {
        private readonly Func<Type, THandles> _builder;
        private readonly Predicate<THandles> _validator;
        private readonly string _logTag;
        private readonly string _logSubject;

        private THandles _handles;
        private bool _initialized;

        public ReflectionCache(
            Func<Type, THandles> builder,
            Predicate<THandles> validator,
            string logTag,
            string logSubject)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logTag = logTag;
            _logSubject = logSubject;
        }

        public bool IsInitialized => _initialized;

        /// <summary>Non-null iff <see cref="IsInitialized"/> is true.</summary>
        public THandles Handles => _handles;

        /// <summary>
        /// Builds the handles on first successful call and caches them. Subsequent calls are no-ops
        /// and return the cached result. Returns true iff the validator passed.
        /// </summary>
        public bool EnsureInitialized(Type typeToInspect)
        {
            if (_initialized) return true;
            if (typeToInspect == null) return false;

            THandles candidate;
            try
            {
                candidate = _builder(typeToInspect);
            }
            catch (Exception ex)
            {
                Log.Error(_logTag, $"Failed to initialize {_logSubject} reflection: {ex.Message}");
                return false;
            }

            if (candidate == null || !_validator(candidate))
            {
                var nullNames = EnumerateNullHandleNames(candidate);
                string joined = nullNames.Count > 0 ? string.Join(", ", nullNames) : "(builder returned null)";
                Log.Warn(_logTag, $"Could not resolve required handles for {_logSubject}: {joined}");
                return false;
            }

            _handles = candidate;
            _initialized = true;
            Log.Msg(_logTag, $"{_logSubject} reflection initialized");
            return true;
        }

        /// <summary>Reset to uninitialized. Used for scene-change cache clearing.</summary>
        public void Clear()
        {
            _handles = null;
            _initialized = false;
        }

        private static List<string> EnumerateNullHandleNames(THandles candidate)
        {
            var result = new List<string>();
            if (candidate == null) return result;
            var fields = typeof(THandles).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (!IsReflectionHandleType(f.FieldType)) continue;
                if (f.GetValue(candidate) == null)
                    result.Add(f.Name);
            }
            return result;
        }

        private static bool IsReflectionHandleType(Type t) =>
            t == typeof(FieldInfo) || t == typeof(PropertyInfo) || t == typeof(MethodInfo) || t == typeof(Type);
    }
}
