namespace TK.Haptics
{
    /// <summary>No-op backend for the Editor and platforms without haptics. Reports unsupported.</summary>
    public sealed class NullHapticBackend : IHapticBackend
    {
        public bool IsSupported => false;
        public bool SystemTouchVibrationDisabled => false;
        public void Impact(HapticImpact strength) { }
        public void Selection() { }
        public void Notification(HapticNotification type) { }
    }
}
