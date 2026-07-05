using TK.Analytics;
using TK.IAP;

namespace TK.Analytics.Samples.IntegrationExamples
{
    /// <summary>
    /// Bridges com.tk.iap's IPurchaseReporter into com.tk.analytics: a confirmed purchase flows through
    /// IAnalytics to every configured backend. Wire: iapOptions.Reporter = new AnalyticsPurchaseReporter(analytics).
    /// IPurchaseReporter.OnPurchaseConfirmed is at-least-once (crash-recovery redelivery) — dedupe downstream
    /// by TransactionId. References TK.IAP + TK.Analytics; compiles only when both packages are present.
    /// </summary>
    public sealed class AnalyticsPurchaseReporter : IPurchaseReporter
    {
        private readonly IAnalytics _analytics;
        public AnalyticsPurchaseReporter(IAnalytics analytics) => _analytics = analytics;

        public void OnPurchaseConfirmed(IapPurchaseInfo info) =>
            _analytics.LogPurchase(new AnalyticsPurchase(
                info.ProductId, (double)info.LocalizedPrice, info.IsoCurrencyCode,
                info.TransactionId, 1, info.IsRestore));

        public void OnPurchaseFailed(string productId, string reason) =>
            _analytics.LogEvent("purchase_failed",
                AnalyticsParam.String("product_id", productId),
                AnalyticsParam.String("reason", reason));
    }
}
