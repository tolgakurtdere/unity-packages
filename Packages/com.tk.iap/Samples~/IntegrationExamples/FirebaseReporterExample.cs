using TK.IAP;
using UnityEngine;

namespace TK.IAP.Samples.IntegrationExamples
{
    /// <summary>
    /// Reference <see cref="IPurchaseReporter"/> implementation that reports confirmed purchases to
    /// Firebase Analytics. Compiles WITHOUT the Firebase SDK present — the actual Firebase calls are
    /// commented out below; uncomment them once Firebase Analytics is installed in your project.
    ///
    /// <see cref="IPurchaseReporter.OnPurchaseConfirmed"/> may fire more than once for the same
    /// transaction (crash-recovery redelivery re-confirms already-applied purchases) — this is
    /// an at-least-once contract, so a real backend/analytics call MUST dedupe downstream using
    /// <see cref="IapPurchaseInfo.TransactionId"/> (e.g. as an idempotency key on your server, or by
    /// tracking already-reported transaction ids locally before sending the analytics event).
    /// </summary>
    public sealed class FirebaseReporterExample : IPurchaseReporter
    {
        // Event/parameter names follow the project's existing Firebase convention.
        private const string PurchaseVerifiedEvent = "c_purchase_verified";
        private const string ParamProductId = "product_id";
        private const string ParamPrice = "price";
        private const string ParamCurrency = "currency";

        public void OnPurchaseConfirmed(IapPurchaseInfo info)
        {
            Debug.Log($"[FirebaseReporterExample] Reporting '{PurchaseVerifiedEvent}' for '{info.ProductId}' " +
                      $"(tx: {info.TransactionId}, restore: {info.IsRestore}).");

            // FirebaseAnalytics.LogEvent(PurchaseVerifiedEvent,
            //     new Parameter(ParamProductId, info.ProductId),
            //     new Parameter(ParamPrice, (double)info.LocalizedPrice),
            //     new Parameter(ParamCurrency, info.IsoCurrencyCode ?? ""));

            // Optional: forward info.NativeOrder (Unity IAP's Order object) to your backend for
            // server-side receipt validation. Dedupe there by info.TransactionId too — the backend
            // sees the same at-least-once contract this method does.
        }

        public void OnPurchaseFailed(string productId, string reason)
        {
            Debug.Log($"[FirebaseReporterExample] Purchase failed for '{productId}': {reason}.");

            // FirebaseAnalytics.LogEvent("c_purchase_failed",
            //     new Parameter(ParamProductId, productId),
            //     new Parameter("reason", reason));
        }
    }
}
