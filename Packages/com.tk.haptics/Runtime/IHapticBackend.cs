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
        /// off, the system drops TOUCH-usage haptics (Selection); MEDIA and NOTIFICATION usages still
        /// play. False wherever the setting can't be read — deliberately NOT folded into
        /// <see cref="IsSupported"/>, which keeps meaning permission + hardware.
        /// </summary>
        bool SystemTouchVibrationDisabled { get; }

        void Impact(HapticImpact strength);
        void Selection();
        void Notification(HapticNotification type);
    }
}
