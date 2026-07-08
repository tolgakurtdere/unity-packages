using System;
using System.Collections.Generic;
using TK.Core.Save;
using UnityEngine;

namespace TK.Haptics
{
    /// <summary>
    /// Cross-platform haptic feedback. Construct once at your composition root, register it in your
    /// service container, and optionally bind it to the static <see cref="Haptics"/> façade. Every
    /// call is a no-op when disabled, throttled, or on a platform without haptics (the Editor is
    /// always a no-op — <see cref="IsSupported"/> is false there). Haptics fire on-device only.
    /// </summary>
    public sealed class HapticService
    {
        private const string SaveKey = "tk_haptics_settings";

        [Serializable]
        internal sealed class HapticSettingsData
        {
            public bool Enabled = true;
        }

        // Throttle namespaces so a Selection and an Impact.Heavy don't throttle each other, but a
        // rapidly-repeated identical haptic does.
        private const int ImpactBase = 0;
        private const int SelectionKey = 100;
        private const int NotificationBase = 200;

        private readonly ISaveSystem _saveSystem;
        private readonly IHapticBackend _backend;
        private readonly HapticSettingsData _data;
        private readonly Dictionary<int, float> _lastFiredAt = new();

        public HapticService(ISaveSystem saveSystem = null, IHapticBackend backend = null)
        {
            _saveSystem = saveSystem;
            _backend = backend ?? CreatePlatformBackend();
            _data = saveSystem != null ? saveSystem.Load(SaveKey, new HapticSettingsData()) : new HapticSettingsData();
        }

        /// <summary>Raised after <see cref="Enabled"/> actually changes — bind a Vibration toggle to it.</summary>
        public event Action Changed;

        /// <summary>Durable on/off. Persisted via the save system when one was supplied; else runtime-only.</summary>
        public bool Enabled
        {
            get => _data.Enabled;
            set
            {
                if (_data.Enabled == value) return;
                _data.Enabled = value;
                _saveSystem?.Save(SaveKey, _data);
                Changed?.Invoke();
            }
        }

        /// <summary>True when the active backend actually vibrates — hide a Vibration toggle where it's false.</summary>
        public bool IsSupported => _backend.IsSupported;

        /// <summary>Minimum unscaled seconds between two identical haptics (0 disables throttling).</summary>
        public float HapticThrottleSeconds { get; set; } = 0.03f;

        public void Impact(HapticImpact strength)
        {
            if (TryFire(ImpactBase + (int)strength)) _backend.Impact(strength);
        }

        public void Selection()
        {
            if (TryFire(SelectionKey)) _backend.Selection();
        }

        public void Notification(HapticNotification type)
        {
            if (TryFire(NotificationBase + (int)type)) _backend.Notification(type);
        }

        private bool TryFire(int throttleKey)
        {
            if (!_data.Enabled || !_backend.IsSupported) return false;

            if (HapticThrottleSeconds > 0f)
            {
                var now = Time.unscaledTime;
                if (_lastFiredAt.TryGetValue(throttleKey, out var last) && now - last < HapticThrottleSeconds)
                    return false;
                _lastFiredAt[throttleKey] = now;
            }

            return true;
        }

        private static IHapticBackend CreatePlatformBackend()
        {
            // The reals compile only under their platform defines, so the Editor and the harness
            // always get the no-op.
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHapticBackend();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IosHapticBackend();
#else
            return new NullHapticBackend();
#endif
        }
    }
}
