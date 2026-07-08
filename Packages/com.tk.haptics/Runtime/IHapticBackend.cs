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

        void Impact(HapticImpact strength);
        void Selection();
        void Notification(HapticNotification type);
    }
}
