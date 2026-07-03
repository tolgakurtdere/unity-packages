using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TK.IAP
{
    /// <summary>
    /// Unity IAP (com.unity.purchasing v5) backed store gateway. Placeholder — the real
    /// implementation lands in Task 5. Tests inject FakeStoreGateway, so nothing executes this.
    /// </summary>
    public sealed class UnityIapGateway : IStoreGateway
    {
#pragma warning disable 0067 // placeholder events are never raised until Task 5 implements them
        public event Action Connected;
        public event Action<string> ConnectionFailed;
        public event Action<IReadOnlyList<StoreProduct>> ProductsFetched;
        public event Action<string> ProductsFetchFailed;
        public event Action<PendingPurchase> PurchasePending;
        public event Action<ConfirmedPurchase> PurchaseConfirmed;
        public event Action<FailedPurchase> PurchaseFailed;
        public event Action<IReadOnlyList<ConfirmedPurchase>> PurchasesFetched;
#pragma warning restore 0067

        public Task ConnectAsync()
            => throw new NotImplementedException("Implemented in Task 5");

        public void FetchProducts(IReadOnlyList<StoreProductDefinition> definitions)
            => throw new NotImplementedException("Implemented in Task 5");

        public void FetchPurchases()
            => throw new NotImplementedException("Implemented in Task 5");

        public void Purchase(string storeId)
            => throw new NotImplementedException("Implemented in Task 5");

        public void Confirm(PendingPurchase pending)
            => throw new NotImplementedException("Implemented in Task 5");

        public bool TryGetProduct(string storeId, out StoreProduct product)
            => throw new NotImplementedException("Implemented in Task 5");
    }
}
