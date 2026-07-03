using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TK.IAP
{
    /// <summary>
    /// Store backend seam. The package ships UnityIapGateway (Unity IAP v5); tests and
    /// special builds may inject their own. All events fire on the main thread.
    /// </summary>
    public interface IStoreGateway
    {
        event Action Connected;
        event Action<string> ConnectionFailed;
        event Action<IReadOnlyList<StoreProduct>> ProductsFetched;
        event Action<string> ProductsFetchFailed;
        event Action<PendingPurchase> PurchasePending;
        event Action<ConfirmedPurchase> PurchaseConfirmed;
        event Action<FailedPurchase> PurchaseFailed;
        /// <summary>Purchase history arrived (restore / startup fetch). Confirmed orders only.</summary>
        event Action<IReadOnlyList<ConfirmedPurchase>> PurchasesFetched;

        Task ConnectAsync();
        void FetchProducts(IReadOnlyList<StoreProductDefinition> definitions);
        void FetchPurchases();
        void Purchase(string storeId);
        void Confirm(PendingPurchase pending);
        bool TryGetProduct(string storeId, out StoreProduct product);
    }
}
