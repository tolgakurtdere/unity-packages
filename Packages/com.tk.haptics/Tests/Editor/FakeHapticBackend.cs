using System.Collections.Generic;

namespace TK.Haptics.Tests
{
    /// <summary>Records the haptics the service dispatches, so tests assert routing without a device.</summary>
    internal sealed class FakeHapticBackend : IHapticBackend
    {
        public readonly List<string> Calls = new();
        public bool Supported = true;

        public bool IsSupported => Supported;

        public void Impact(HapticImpact strength) => Calls.Add($"Impact:{strength}");
        public void Selection() => Calls.Add("Selection");
        public void Notification(HapticNotification type) => Calls.Add($"Notification:{type}");
    }
}
