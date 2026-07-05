using UnityEngine;

namespace TK.Analytics
{
    /// <summary>
    /// Ambient static access point for analytics logging from anywhere. Set one IAnalytics at bootstrap
    /// via SetInstance; every log verb forwards to it. When no instance is set, calls are no-ops (with a
    /// one-time editor warning) so untested/isolated scenes never NullReference. Lifecycle/config lives on
    /// the instance, not here. Tests drive the instance directly and Clear between cases.
    /// </summary>
    public static class Analytics
    {
        public static IAnalytics Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public static void SetInstance(IAnalytics instance) => Instance = instance;
        public static void ClearInstance() => Instance = null;

        public static void LogEvent(string name)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogEvent(name);
        }

        public static void LogEvent(string name, params AnalyticsParam[] parameters)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogEvent(name, parameters);
        }

        public static void LogPurchase(AnalyticsPurchase purchase)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogPurchase(purchase);
        }

        public static void LogAdRevenue(AnalyticsAdRevenue adRevenue)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogAdRevenue(adRevenue);
        }

        public static void SetUserProperty(string key, string value)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.SetUserProperty(key, value);
        }

        public static void SetUserId(string userId)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.SetUserId(userId);
        }

        private static bool s_warnedUnset;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void WarnUnset()
        {
            if (s_warnedUnset) return;
            s_warnedUnset = true;
            Debug.LogWarning("[Analytics] No instance set (call Analytics.SetInstance). Calls are no-ops until one is set.");
        }
    }
}
