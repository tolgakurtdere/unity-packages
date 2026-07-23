using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK.Haptics
{
    /// <summary>
    /// Cross-platform haptic feedback. Construct once at your composition root, register it in your
    /// service container, and optionally bind it to the static <see cref="Haptics"/> façade. Every
    /// call is a no-op when disabled, throttled, or on a platform without haptics (the Editor is
    /// always a no-op — <see cref="IsSupported"/> is false there). Haptics fire on-device only.
    /// Standalone: no package dependencies. <see cref="Enabled"/> is runtime state the game owns —
    /// push it from your settings and persist it however you like (subscribe to <see cref="Changed"/>).
    /// </summary>
    public sealed class HapticService
    {
        // Throttle namespaces so a Selection and an Impact.Heavy don't throttle each other, but a
        // rapidly-repeated identical haptic does.
        private const int ImpactBase = 0;
        private const int SelectionKey = 100;
        private const int NotificationBase = 200;

        private readonly IHapticBackend _backend;
        private readonly Dictionary<int, float> _lastFiredAt = new();
        private bool _enabled = true;

        public HapticService(IHapticBackend backend = null)
        {
            _backend = backend ?? CreatePlatformBackend();
        }

        /// <summary>Raised after <see cref="Enabled"/> actually changes — mirror a Vibration toggle to it if code elsewhere toggles haptics.</summary>
        public event Action Changed;

        /// <summary>On/off, game-owned runtime state (default true). Persist it yourself — the package doesn't.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                Changed?.Invoke();
            }
        }

        /// <summary>True when the active backend actually vibrates — hide a Vibration toggle where it's false.</summary>
        public bool IsSupported => _backend.IsSupported;

        /// <summary>
        /// Best-effort advisory (Android): true while the OS touch-vibration setting is off, in which case
        /// TOUCH-usage haptics (Selection) are dropped by the system unless
        /// <see cref="BypassSystemVibrationSetting"/> is on. Use it to show a "turn on vibration in system
        /// settings" hint — not to hide the toggle; <see cref="IsSupported"/> still owns that. False on
        /// platforms where the setting can't be read. Read it live: the user can change it mid-session.
        /// </summary>
        public bool SystemTouchVibrationDisabled => _backend.SystemTouchVibrationDisabled;

        /// <summary>
        /// Opt-in (default false, Android-only effect): mark this game's vibrations to bypass the OS
        /// vibration preference. Rationale for using it: the game's own Vibration toggle is the player's
        /// consent surface. It rides on a non-public platform flag — OEM/version dependent; when the
        /// platform strips it, haptics degrade to the per-usage classification instead of vanishing.
        /// </summary>
        public bool BypassSystemVibrationSetting
        {
            get => _backend.BypassSystemVibrationSetting;
            set => _backend.BypassSystemVibrationSetting = value;
        }

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
            if (!_enabled || !_backend.IsSupported) return false;

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
