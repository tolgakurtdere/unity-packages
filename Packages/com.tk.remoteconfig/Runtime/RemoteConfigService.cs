using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.RemoteConfig
{
    /// <summary>
    /// Backend-agnostic remote-config façade. Declare typed params via the factory methods (Task 3),
    /// read them live with default fallback, override in-editor for QA, refresh at runtime.
    /// Main-thread usage assumed. One service per backend; construct fresh to re-init after a failure.
    /// </summary>
    public sealed partial class RemoteConfigService
    {
        public static RemoteConfigService Instance { get; private set; }

        /// <summary>True once defaults are registered (backend init completed) — reads are safe (return cached/default), never crash.</summary>
        public bool IsSafeToRead { get; private set; }

        /// <summary>True after the first successful fetch+activate.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Latch: fires once when ready; subscribing after ready invokes immediately.</summary>
        public event Action OnReady
        {
            add { if (IsReady) value?.Invoke(); else _onReady += value; }
            remove => _onReady -= value;
        }

        /// <summary>Fires on every activation (initial fetch + each refresh that activates new values).</summary>
        public event Action OnChanged;

        private readonly IRemoteConfigBackend _backend;
        private readonly RemoteConfigOptions _options;
        private readonly Dictionary<string, object> _defaults = new();
        private Action _onReady;
        private Task _initTask;

        public RemoteConfigService(IRemoteConfigBackend backend, RemoteConfigOptions options = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _options = options ?? new RemoteConfigOptions();
            Instance = this;
        }

        /// <summary>Records a parameter's default for backend registration. Called by the typed factories.</summary>
        internal void RegisterDefault(string key, object firebaseDefault)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[RemoteConfig] Parameter registered with an empty key.");
                return;
            }

            if (_defaults.TryGetValue(key, out var existing) && !Equals(existing, firebaseDefault))
                Debug.LogWarning($"[RemoteConfig] Key '{key}' registered with a different default; last wins.");

            _defaults[key] = firebaseDefault;
        }

        public async Task InitializeAsync()
        {
            if (_initTask != null)
            {
                await _initTask;
                return;
            }

            _initTask = InitializeInternalAsync();
            await _initTask;
        }

        private async Task InitializeInternalAsync()
        {
            try
            {
                await _backend.InitializeAsync(_defaults);
                IsSafeToRead = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return; // IsSafeToRead stays false → reads return defaults; no throw out of Initialize
            }

            if (_options.FetchOnInitialize)
                await FetchActivateAsync();
        }

        /// <summary>Manual re-fetch+activate. Returns whether new values activated. Never throws.</summary>
        public async Task<bool> RefreshAsync()
        {
            if (!IsSafeToRead)
            {
                Debug.LogWarning("[RemoteConfig] RefreshAsync called before InitializeAsync; ignored.");
                return false;
            }

            return await FetchActivateAsync();
        }

        private async Task<bool> FetchActivateAsync()
        {
            try
            {
                var activated = await _backend.FetchAndActivateAsync();

                if (!IsReady)
                {
                    IsReady = true;
                    var latch = _onReady;
                    _onReady = null;
                    latch?.Invoke();
                }

                if (activated) OnChanged?.Invoke();
                return activated;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        // ── Raw reads (default fallback + safety gate + editor override) ──

        public long GetLong(string key, long def)
        {
            if (TryGetOverride<long>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetLong(key, out var v) ? v : def;
        }

        public int GetInt(string key, int def)
        {
            if (TryGetOverride<int>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetLong(key, out var v) ? unchecked((int)v) : def;
        }

        public double GetDouble(string key, double def)
        {
            if (TryGetOverride<double>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetDouble(key, out var v) ? v : def;
        }

        public float GetFloat(string key, float def)
        {
            if (TryGetOverride<float>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetDouble(key, out var v) ? (float)v : def;
        }

        public bool GetBool(string key, bool def)
        {
            if (TryGetOverride<bool>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetBool(key, out var v) ? v : def;
        }

        public string GetString(string key, string def)
        {
            if (TryGetOverride<string>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetString(key, out var v) ? v : def;
        }

        internal static bool TryGetOverride<T>(string key, out T value)
        {
#if UNITY_EDITOR || TEST_MODE
            if (RemoteConfigDebug.TryGet(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
#endif
            value = default;
            return false;
        }
    }
}
