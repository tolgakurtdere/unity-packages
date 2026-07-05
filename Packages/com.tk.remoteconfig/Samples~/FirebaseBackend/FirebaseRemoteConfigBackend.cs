using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.RemoteConfig;
using TK.RemoteConfig;
using UnityEngine;

namespace TK.RemoteConfig.Samples.FirebaseBackend
{
    /// <summary>
    /// <see cref="IRemoteConfigBackend"/> adapter backed by <b>Firebase Remote Config</b>.
    ///
    /// This is a <b>Sample</b>: it references <c>Firebase.RemoteConfig</c>, so it only compiles once
    /// imported into a project that already has the Firebase Remote Config SDK installed. That SDK is
    /// the <i>game's</i> own dependency — added via Google's Unity registry, the Firebase tarballs, or
    /// the Firebase <c>.unitypackage</c>. The <c>com.tk.remoteconfig</c> package itself has <b>no</b>
    /// Firebase dependency; that is the whole point of the backend seam. Copy this file into your
    /// project (or import the sample) and adapt it.
    ///
    /// Written against the latest stable Firebase Unity SDK (13.x). The Remote Config API used here
    /// is stable across 12.x → 13.x: <c>FirebaseRemoteConfig.DefaultInstance</c>,
    /// <c>SetDefaultsAsync</c>, <c>FetchAsync(TimeSpan)</c>, <c>Info.LastFetchStatus</c>,
    /// <c>ActivateAsync</c>, and <c>GetValue(key)</c> returning a <c>ConfigValue</c>.
    ///
    /// Assumes Firebase is already initialized (dependencies checked / app created) before
    /// <see cref="InitializeAsync"/> is called — the standard place is right after
    /// <c>FirebaseApp.CheckAndFixDependenciesAsync()</c> succeeds. On iOS, Remote Config values are
    /// only safe to read after <c>SetDefaultsAsync</c> has completed; this adapter enforces that by
    /// gating <see cref="IsReady"/> on that call, and the <see cref="RemoteConfigService"/> in turn
    /// gates its reads on the backend being initialized.
    /// </summary>
    public sealed class FirebaseRemoteConfigBackend : IRemoteConfigBackend
    {
        private bool _isReady;

        /// <summary>True after <see cref="InitializeAsync"/> registered defaults (Firebase core is
        /// initialized by that point, so <c>GetValue</c> is safe — cached/default values read even
        /// offline).</summary>
        public bool IsReady => _isReady;

        public async Task InitializeAsync(IReadOnlyDictionary<string, object> defaults)
        {
            // Firebase wants a mutable IDictionary; copy the service's read-only defaults into one.
            await FirebaseRemoteConfig.DefaultInstance
                .SetDefaultsAsync(new Dictionary<string, object>(defaults));

            // Only now are GetValue reads safe (notably on iOS). The service's IsSafeToRead follows.
            _isReady = true;
        }

        public async Task<bool> FetchAndActivateAsync()
        {
            var remoteConfig = FirebaseRemoteConfig.DefaultInstance;

            // TimeSpan.Zero bypasses the fetch cache so console changes show up immediately. For a
            // shipping app you may prefer the default (12h) or a few minutes to respect fetch quotas.
            await remoteConfig.FetchAsync(TimeSpan.Zero);

            if (remoteConfig.Info.LastFetchStatus != LastFetchStatus.Success)
            {
                Debug.LogWarning(
                    $"[FirebaseRemoteConfigBackend] Fetch not successful: {remoteConfig.Info.LastFetchStatus}. " +
                    "Keeping previously activated / default values.");
                return false;
            }

            // ActivateAsync returns Task<bool> in Firebase 12.x/13.x: true if newly fetched values
            // were made active (false when the fetched config equals what is already active).
            return await remoteConfig.ActivateAsync();
        }

        // ── TryGet* ──────────────────────────────────────────────────────────────────────────────
        // CONTRACT: these must NEVER throw. RemoteConfigService's raw reads call them without a
        // try/catch and rely on a false return to fall back to the parameter default. A Firebase
        // ConfigValue getter can throw if the underlying value is not convertible (e.g. reading a
        // non-numeric string as a long), so every accessor is wrapped and returns false on any
        // exception. A missing key (no remote AND no default) surfaces as an empty ByteArrayValue,
        // which is also treated as "no value".

        public bool TryGetLong(string key, out long value)
        {
            try
            {
                var raw = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (HasNoValue(raw))
                {
                    value = 0;
                    return false;
                }

                value = raw.LongValue;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FirebaseRemoteConfigBackend] TryGetLong('{key}') failed: {exception.Message}");
                value = 0;
                return false;
            }
        }

        public bool TryGetDouble(string key, out double value)
        {
            try
            {
                var raw = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (HasNoValue(raw))
                {
                    value = 0;
                    return false;
                }

                value = raw.DoubleValue;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FirebaseRemoteConfigBackend] TryGetDouble('{key}') failed: {exception.Message}");
                value = 0;
                return false;
            }
        }

        public bool TryGetBool(string key, out bool value)
        {
            try
            {
                var raw = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (HasNoValue(raw))
                {
                    value = false;
                    return false;
                }

                value = raw.BooleanValue;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FirebaseRemoteConfigBackend] TryGetBool('{key}') failed: {exception.Message}");
                value = false;
                return false;
            }
        }

        public bool TryGetString(string key, out string value)
        {
            try
            {
                var raw = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (HasNoValue(raw))
                {
                    value = null;
                    return false;
                }

                value = raw.StringValue;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FirebaseRemoteConfigBackend] TryGetString('{key}') failed: {exception.Message}");
                value = null;
                return false;
            }
        }

        /// <summary>
        /// A Firebase <c>ConfigValue</c> for an unknown key (no remote value AND no registered
        /// default) has a null/empty <c>ByteArrayValue</c>. Treat that as "no value" so the service
        /// falls back to the parameter default rather than reading a bogus zero/empty-string.
        /// </summary>
        private static bool HasNoValue(ConfigValue configValue)
        {
            var data = configValue.ByteArrayValue;
            return data == null || !data.Any();
        }
    }
}
