using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using TK.Analytics;

namespace TK.Analytics.Samples.IntegrationExamples
{
    /// <summary>
    /// Reference <see cref="IAnalyticsBackend"/> that forwards to Firebase Analytics. This is a Sample:
    /// it references <c>Firebase.Analytics</c> and therefore compiles only once imported into a project
    /// that has the Firebase Analytics SDK installed. Add it to an <see cref="AnalyticsService"/> like:
    /// <c>new AnalyticsService(new IAnalyticsBackend[] { new FirebaseAnalyticsBackend() })</c>.
    /// Member spellings were verified against the installed Firebase SDK (e.g. <c>ParameterTransactionID</c>).
    /// </summary>
    public sealed class FirebaseAnalyticsBackend : IAnalyticsBackend
    {
        public string Name => "Firebase";

        /// <summary>
        /// Resolves Google Play services dependencies, then Firebase is ready to log. If your game
        /// already initializes Firebase centrally (its own <see cref="FirebaseApp.CheckAndFixDependenciesAsync"/>
        /// on boot), you can drop this call and just <c>return Task.CompletedTask</c> here.
        /// </summary>
        public async Task InitializeAsync()
        {
            await FirebaseApp.CheckAndFixDependenciesAsync();
        }

        public void LogEvent(AnalyticsEvent evt)
        {
            var parameters = ToParameters(evt.Parameters);
            FirebaseAnalytics.LogEvent(evt.Name, parameters);
        }

        /// <summary>
        /// Logs a GA4 <c>purchase</c> event. Cross-platform on purpose — the common reference guards
        /// this with <c>#if UNITY_IOS</c> and so silently drops Android purchases from Firebase; this
        /// runs on every platform.
        /// </summary>
        public void LogPurchase(AnalyticsPurchase p)
        {
            var items = new IDictionary<string, object>[]
            {
                new Dictionary<string, object>
                {
                    { FirebaseAnalytics.ParameterItemID, p.ProductId },
                    { FirebaseAnalytics.ParameterQuantity, (long)p.Quantity }
                }
            };

            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, new[]
            {
                new Parameter(FirebaseAnalytics.ParameterTransactionID, p.TransactionId),
                new Parameter(FirebaseAnalytics.ParameterCurrency, p.Currency ?? ""),
                new Parameter(FirebaseAnalytics.ParameterValue, p.Price),
                new Parameter(FirebaseAnalytics.ParameterItems, items)
            });
        }

        /// <summary>
        /// Logs Firebase's recommended <c>ad_impression</c> event. <c>ad_platform</c> is the mediation
        /// platform — hard-coded "AppLovin" because com.tk.ads sources revenue from AppLovin MAX; change
        /// it if you feed non-MAX ad revenue through analytics.
        /// </summary>
        public void LogAdRevenue(AnalyticsAdRevenue a)
        {
            FirebaseAnalytics.LogEvent("ad_impression", new[]
            {
                new Parameter("ad_platform", "AppLovin"),
                new Parameter("ad_source", a.AdNetwork ?? ""),
                new Parameter("ad_unit_name", a.AdUnitId ?? ""),
                new Parameter("ad_format", a.Format ?? ""),
                new Parameter("value", a.Revenue),
                new Parameter("currency", a.Currency ?? "")
            });
        }

        public void SetUserProperty(string key, string value) => FirebaseAnalytics.SetUserProperty(key, value);

        public void SetUserId(string userId) => FirebaseAnalytics.SetUserId(userId);

        /// <summary>No-op: Firebase batches and flushes events itself; there is no manual flush API.</summary>
        public void Flush() { }

        private static Parameter[] ToParameters(IReadOnlyList<AnalyticsParam> source)
        {
            var parameters = new Parameter[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var p = source[i];
                parameters[i] = p.Type switch
                {
                    AnalyticsParamType.String => new Parameter(p.Key, p.StringValue),
                    AnalyticsParamType.Long   => new Parameter(p.Key, p.LongValue),
                    AnalyticsParamType.Double => new Parameter(p.Key, p.DoubleValue),
                    AnalyticsParamType.Bool   => new Parameter(p.Key, p.BoolValue ? "true" : "false"),
                    _                         => new Parameter(p.Key, p.StringValue ?? "")
                };
            }
            return parameters;
        }
    }
}
