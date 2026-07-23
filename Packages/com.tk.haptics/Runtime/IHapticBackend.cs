namespace TK.Haptics
{
    /// <summary>
    /// Platform haptic seam. The package ships native Android/iOS backends (compiled only under
    /// their platform defines) and a no-op fallback; tests inject a fake. Implementations must
    /// never throw into game code.
    /// </summary>
    public interface IHapticBackend
    {
        /// <summary>True when this backend actually produces haptics (false in the Editor / unsupported platforms).</summary>
        bool IsSupported { get; }

        /// <summary>
        /// Best-effort advisory: true when the OS-level touch-vibration setting is off (Android). With it
        /// off, TOUCH-usage haptics (Selection) are dropped by the system unless
        /// <see cref="BypassSystemVibrationSetting"/> is on. False wherever the setting can't be read —
        /// deliberately NOT folded into <see cref="IsSupported"/>, which keeps meaning permission + hardware.
        /// </summary>
        bool SystemTouchVibrationDisabled { get; }

        /// <summary>
        /// Opt-in (default false): mark vibrations to bypass the user's OS vibration preference
        /// (Android, via a non-public attribute flag — OEM/version dependent; when the platform strips
        /// it, behavior degrades to the honest per-usage classification). No effect elsewhere.
        /// </summary>
        bool BypassSystemVibrationSetting { get; set; }

        void Impact(HapticImpact strength);
        void Selection();
        void Notification(HapticNotification type);
    }
}
