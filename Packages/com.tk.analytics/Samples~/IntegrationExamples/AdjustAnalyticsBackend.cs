using System.Collections.Generic;
using System.Threading.Tasks;
using AdjustSdk;
using TK.Analytics;

namespace TK.Analytics.Samples.IntegrationExamples
{
    /// <summary>
    /// Reference <see cref="IAnalyticsBackend"/> that forwards to Adjust. This is a Sample: it references
    /// <c>AdjustSdk</c> and therefore compiles only once imported into a project that has the Adjust SDK
    /// installed. A <b>selective</b> backend — Adjust tracks a curated set of milestone events keyed by
    /// their Adjust event token, not every analytics event. Pass the name→token map (and an optional
    /// purchase token) at construction; events without a mapped token are ignored.
    /// Member spellings were verified against the installed Adjust SDK (namespace <c>AdjustSdk</c>,
    /// <c>AdjustAdRevenue</c> with <c>AdRevenueNetwork</c>/<c>AdRevenueUnit</c>/<c>AdRevenuePlacement</c>).
    /// </summary>
    public sealed class AdjustAnalyticsBackend : IAnalyticsBackend
    {
        private readonly IReadOnlyDictionary<string, string> _eventTokens;
        private readonly string _purchaseEventToken;

        /// <param name="eventTokens">Maps an analytics event name to its Adjust event token. Events not
        /// present here are not sent to Adjust.</param>
        /// <param name="purchaseEventToken">Adjust token for the purchase revenue event; leave null to
        /// not forward purchases to Adjust.</param>
        public AdjustAnalyticsBackend(IReadOnlyDictionary<string, string> eventTokens = null,
            string purchaseEventToken = null)
        {
            _eventTokens = eventTokens;
            _purchaseEventToken = purchaseEventToken;
        }

        public string Name => "Adjust";

        /// <summary>No-op: the game initializes Adjust centrally via its own config/SDK start.</summary>
        public Task InitializeAsync() => Task.CompletedTask;

        public void LogEvent(AnalyticsEvent evt)
        {
            if (_eventTokens != null && _eventTokens.TryGetValue(evt.Name, out var token))
                Adjust.TrackEvent(new AdjustEvent(token));
            // else: not a mapped milestone event — Adjust ignores it by design.
        }

        public void LogPurchase(AnalyticsPurchase p)
        {
            if (_purchaseEventToken == null) return;
            var e = new AdjustEvent(_purchaseEventToken);
            e.SetRevenue(p.Price, p.Currency);
            Adjust.TrackEvent(e);
        }

        public void LogAdRevenue(AnalyticsAdRevenue a)
        {
            var r = new AdjustAdRevenue("applovin_max_sdk");
            r.SetRevenue(a.Revenue, a.Currency);
            r.AdRevenueNetwork = a.AdNetwork;
            r.AdRevenueUnit = a.AdUnitId;
            r.AdRevenuePlacement = a.Placement;
            Adjust.TrackAdRevenue(r);
        }

        /// <summary>
        /// Adjust has no first-class user properties — this maps a property to a global callback
        /// parameter, appended to every subsequent Adjust callback/attribution payload.
        /// </summary>
        public void SetUserProperty(string key, string value) => Adjust.AddGlobalCallbackParameter(key, value);

        /// <summary>Adjust has no dedicated user-id API here — carried as a "user_id" global callback parameter.</summary>
        public void SetUserId(string userId) => Adjust.AddGlobalCallbackParameter("user_id", userId);

        /// <summary>No-op: Adjust sends events itself; there is no manual flush API.</summary>
        public void Flush() { }
    }
}
