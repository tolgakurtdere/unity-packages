using System;

namespace TK.RemoteConfig
{
    /// <summary>
    /// A strongly-typed, declared-once config parameter. Reads through its RemoteConfigService
    /// (default fallback + safety gate + editor override), converts implicitly to T.
    /// Create via the service factories (rc.Int/Bool/String/...).
    /// </summary>
    public sealed class ConfigParam<T>
    {
        public string Key { get; }
        public T Default { get; }

        private readonly Func<T> _getter;

        internal ConfigParam(string key, T def, Func<T> getter)
        {
            Key = key;
            Default = def;
            _getter = getter;
        }

        /// <summary>Current value: editor override (if any) → backend value → default.</summary>
        public T Value => _getter();

        public static implicit operator T(ConfigParam<T> param) => param.Value;

        public override string ToString() => Value?.ToString() ?? string.Empty;

#if UNITY_EDITOR || TEST_MODE
        public bool HasDebugOverride => RemoteConfigDebug.TryGet(Key, out _);
        public void SetDebugOverride(T value) => RemoteConfigDebug.Set(Key, value);
        public void ClearDebugOverride() => RemoteConfigDebug.Clear(Key);
#endif
    }
}
