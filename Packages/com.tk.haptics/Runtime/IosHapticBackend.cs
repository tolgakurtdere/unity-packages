#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace TK.Haptics
{
    /// <summary>
    /// iOS haptics via UIFeedbackGenerator, bridged through the embedded Plugins/iOS/TKHaptics.mm.
    /// Always supported on iOS hardware. Device-verified.
    /// </summary>
    public sealed class IosHapticBackend : IHapticBackend
    {
        [DllImport("__Internal")] private static extern void _TKHapticImpact(int style);
        [DllImport("__Internal")] private static extern void _TKHapticSelection();
        [DllImport("__Internal")] private static extern void _TKHapticNotification(int type);

        public bool IsSupported => true;

        public void Impact(HapticImpact strength) => _TKHapticImpact((int)strength);
        public void Selection() => _TKHapticSelection();
        public void Notification(HapticNotification type) => _TKHapticNotification((int)type);
    }
}
#endif
