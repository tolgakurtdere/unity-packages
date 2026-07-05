using TK.Ads;
using TK.Analytics;

namespace TK.Analytics.Samples.IntegrationExamples
{
    /// <summary>
    /// Bridges com.tk.ads's IAdRevenueReporter into com.tk.analytics: each paid impression flows through
    /// IAnalytics to every configured backend. Wire: adsOptions.RevenueReporter = new AnalyticsAdRevenueReporter(analytics).
    /// References TK.Ads + TK.Analytics; compiles only when both packages are present.
    /// </summary>
    public sealed class AnalyticsAdRevenueReporter : IAdRevenueReporter
    {
        private readonly IAnalytics _analytics;
        public AnalyticsAdRevenueReporter(IAnalytics analytics) => _analytics = analytics;

        public void OnAdRevenue(AdRevenueInfo info) =>
            _analytics.LogAdRevenue(new AnalyticsAdRevenue(
                info.Format, info.NetworkName, info.AdUnitId, info.Revenue, info.Currency, info.Placement));
    }
}
