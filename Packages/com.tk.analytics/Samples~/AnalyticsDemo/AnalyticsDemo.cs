using TK.Analytics;
using UnityEngine;

namespace TK.Analytics.Samples.AnalyticsDemo
{
    /// <summary>
    /// Editor-runnable demo of the full <see cref="AnalyticsService"/> flow on the built-in
    /// <see cref="ConsoleAnalyticsBackend"/> — no SDK required. Attach to any GameObject and press Play,
    /// then drive it from the <c>[ContextMenu]</c> entries (right-click the component header in the
    /// Inspector, or the component's "⋮" menu).
    ///
    /// To see the consent gate + loss-free buffer in action: press Play, then <c>Log Test Event</c>
    /// BEFORE <c>Start Backends</c> and <c>Grant Consent</c> — nothing prints, because the event is
    /// buffered (dispatch isn't allowed until the service is started AND consent is granted). Now run
    /// <c>Start Backends</c> then <c>Grant Consent</c>: the buffered event flushes to the Console in
    /// order, with its parameters intact. Run <c>Deny Consent</c> instead and the buffer is cleared
    /// (GDPR-safe) — the event is dropped, never sent.
    /// </summary>
    public class AnalyticsDemo : MonoBehaviour
    {
        private AnalyticsService _analytics;

        private void Awake()
        {
            _analytics = new AnalyticsService(new IAnalyticsBackend[] { new ConsoleAnalyticsBackend() });
            Analytics.SetInstance(_analytics);
            Debug.Log("[AnalyticsDemo] Ready. Log an event BEFORE 'Start Backends'/'Grant Consent' to watch it buffer, " +
                      "then flush once both are done. See the ContextMenu entries.");
        }

        // ── Lifecycle & consent ──

        [ContextMenu("Start Backends")]
        private void StartBackends()
        {
            Debug.Log("[AnalyticsDemo] StartAsync() — initializing backends.");
            _ = _analytics.StartAsync();
        }

        [ContextMenu("Grant Consent")]
        private void GrantConsent()
        {
            Debug.Log("[AnalyticsDemo] SetConsent(true) — buffered events (if started) flush now, in order.");
            _analytics.SetConsent(true);
        }

        [ContextMenu("Deny Consent")]
        private void DenyConsent()
        {
            Debug.Log("[AnalyticsDemo] SetConsent(false) — buffer cleared; nothing is sent.");
            _analytics.SetConsent(false);
        }

        // ── Logging ──

        [ContextMenu("Log Test Event")]
        private void LogTestEvent() =>
            Analytics.LogEvent("demo_event", AnalyticsParam.Long("score", 42), AnalyticsParam.String("mode", "hard"));

        [ContextMenu("Log Test Purchase")]
        private void LogTestPurchase() =>
            Analytics.LogPurchase(new AnalyticsPurchase("com.demo.coins", 4.99, "USD", "demo-tx-1"));

        [ContextMenu("Log Test Ad Revenue")]
        private void LogTestAdRevenue() =>
            Analytics.LogAdRevenue(new AnalyticsAdRevenue("rewarded", "admob", "demo_unit", 0.02, "USD", "level_end"));

        [ContextMenu("Set User")]
        private void SetUser()
        {
            Analytics.SetUserId("demo-user");
            Analytics.SetUserProperty("tier", "gold");
        }

        [ContextMenu("Flush")]
        private void Flush() => _analytics.Flush();
    }
}
