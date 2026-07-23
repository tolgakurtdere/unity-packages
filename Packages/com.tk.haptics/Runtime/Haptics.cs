using UnityEngine;

namespace TK.Haptics
{
    /// <summary>
    /// Static façade over a bound <see cref="HapticService"/> (the Analytics/Audio pattern):
    /// <c>Haptics.Bind(service)</c> at the composition root, then <c>Haptics.Selection()</c>
    /// anywhere. Unbound calls warn once and no-op.
    /// </summary>
    public static class Haptics
    {
        private static HapticService s_service;
        private static bool s_warned;

        // With Enter Play Mode's domain reload disabled, statics survive between sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_service = null;
            s_warned = false;
        }

        /// <summary>The bound service; null when none.</summary>
        public static HapticService Service => s_service;

        public static void Bind(HapticService service)
        {
            s_service = service;
            s_warned = false;
        }

        /// <summary>Unbinds only when <paramref name="service"/> is the currently bound one.</summary>
        public static void Unbind(HapticService service)
        {
            if (ReferenceEquals(s_service, service)) s_service = null;
        }

        /// <summary>True when the bound service's backend actually vibrates (false when unbound / in the Editor).</summary>
        public static bool IsSupported => s_service?.IsSupported ?? false;

        public static bool Enabled
        {
            get => s_service?.Enabled ?? true;
            set { var service = Resolve(); if (service != null) service.Enabled = value; }
        }

        /// <summary>Mirror of <see cref="HapticService.SystemTouchVibrationDisabled"/>; false when unbound.</summary>
        public static bool SystemTouchVibrationDisabled => s_service?.SystemTouchVibrationDisabled ?? false;

        /// <summary>Mirror of <see cref="HapticService.BypassSystemVibrationSetting"/>; false / no-op when unbound.</summary>
        public static bool BypassSystemVibrationSetting
        {
            get => s_service?.BypassSystemVibrationSetting ?? false;
            set { var service = Resolve(); if (service != null) service.BypassSystemVibrationSetting = value; }
        }

        public static void Impact(HapticImpact strength) => Resolve()?.Impact(strength);
        public static void Selection() => Resolve()?.Selection();
        public static void Notification(HapticNotification type) => Resolve()?.Notification(type);

        private static HapticService Resolve()
        {
            if (s_service == null && !s_warned)
            {
                s_warned = true;
                Debug.LogWarning("[Haptics] No HapticService bound — call Haptics.Bind(service) at your composition root.");
            }

            return s_service;
        }
    }
}
