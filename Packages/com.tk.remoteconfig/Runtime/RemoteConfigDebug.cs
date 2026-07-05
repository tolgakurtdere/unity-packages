using System.Collections.Generic;

namespace TK.RemoteConfig
{
    /// <summary>
    /// Editor/TEST_MODE-only session override store for QA. Never compiled into release builds.
    /// A game wires this to its own debug menu (see the Integration Examples sample).
    /// </summary>
    public static class RemoteConfigDebug
    {
#if UNITY_EDITOR || TEST_MODE
        private static readonly Dictionary<string, object> s_overrides = new();

        public static bool HasAny => s_overrides.Count > 0;
        public static void Set(string key, object value) => s_overrides[key] = value;
        public static void Clear(string key) => s_overrides.Remove(key);
        public static void ClearAll() => s_overrides.Clear();
        public static bool TryGet(string key, out object value) => s_overrides.TryGetValue(key, out value);
#else
        public static bool HasAny => false;
        public static void Set(string key, object value) { }
        public static void Clear(string key) { }
        public static void ClearAll() { }
        public static bool TryGet(string key, out object value) { value = null; return false; }
#endif
    }
}
