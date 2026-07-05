using TK.RemoteConfig;
using UnityEngine;

namespace TK.RemoteConfig.Samples.IntegrationExamples
{
    /// <summary>
    /// Shows how a game wires <c>com.tk.remoteconfig</c>'s editor overrides to its own QA / debug
    /// menu. Here the hooks are Unity's built-in <see cref="ContextMenu"/> (right-click the component
    /// in the Inspector) — in a real game you would call the same
    /// <c>param.SetDebugOverride(...)</c> / <c>RemoteConfigDebug.ClearAll()</c> from your debug-menu
    /// framework (SROptions, a runtime cheat panel, etc.).
    ///
    /// Overrides are <b>editor / TEST_MODE only</b> — <see cref="RemoteConfigDebug"/> compiles to a
    /// no-op in release, so this wiring never ships live. An override wins over the backend value AND
    /// the default, on both the typed <c>param.Value</c> path and the raw <c>rc.GetInt</c> path, so
    /// QA can force any value without a backend round-trip.
    ///
    /// Attach to a GameObject; a <see cref="RemoteConfigService"/> must have been constructed
    /// elsewhere (its <c>Instance</c> is used to declare the example params).
    /// </summary>
    public sealed class RemoteConfigDebugMenuExample : MonoBehaviour
    {
        private ConfigParam<int> _interstitialInterval;
        private ConfigParam<bool> _hardModeEnabled;

        private void Awake()
        {
            var rc = RemoteConfigService.Instance;
            if (rc == null)
            {
                Debug.LogWarning("[RemoteConfigDebugMenu] No RemoteConfigService.Instance — construct one first.");
                return;
            }

            // Declaring params here is just for the sample; a real game declares them once in a config
            // holder and hands references to its debug menu.
            _interstitialInterval = rc.Int("interstitial_interval", 30);
            _hardModeEnabled = rc.Bool("hard_mode_enabled", false);
        }

#if UNITY_EDITOR || TEST_MODE
        [ContextMenu("RC Debug/Force interstitial interval = 5s")]
        private void ForceShortInterval()
        {
            _interstitialInterval?.SetDebugOverride(5);
            Debug.Log($"[RemoteConfigDebugMenu] interstitial_interval override → {_interstitialInterval?.Value}s");
        }

        [ContextMenu("RC Debug/Enable hard mode")]
        private void ForceHardMode()
        {
            _hardModeEnabled?.SetDebugOverride(true);
            Debug.Log($"[RemoteConfigDebugMenu] hard_mode_enabled override → {_hardModeEnabled?.Value}");
        }

        [ContextMenu("RC Debug/Clear interstitial interval override")]
        private void ClearIntervalOverride()
        {
            _interstitialInterval?.ClearDebugOverride();
            Debug.Log($"[RemoteConfigDebugMenu] interstitial_interval restored → {_interstitialInterval?.Value}s");
        }

        [ContextMenu("RC Debug/Clear ALL overrides")]
        private void ClearAllOverrides()
        {
            RemoteConfigDebug.ClearAll();
            Debug.Log("[RemoteConfigDebugMenu] Cleared all remote-config debug overrides.");
        }
#endif
    }
}
