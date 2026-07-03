namespace TK.IAP
{
    /// <summary>Everything analytics/backend needs about a confirmed purchase.</summary>
    public readonly struct IapPurchaseInfo
    {
        public string ProductId { get; }        // catalog id
        public string StoreId { get; }
        public string TransactionId { get; }
        public decimal LocalizedPrice { get; }  // 0 when store metadata unavailable
        public string IsoCurrencyCode { get; }  // null when unavailable
        public bool IsRestore { get; }
        /// <summary>Backend-native order object (Unity IAP Order) for server receipt validation. May be null.</summary>
        public object NativeOrder { get; }

        public IapPurchaseInfo(string productId, string storeId, string transactionId,
            decimal localizedPrice, string isoCurrencyCode, bool isRestore, object nativeOrder)
        {
            ProductId = productId;
            StoreId = storeId;
            TransactionId = transactionId;
            LocalizedPrice = localizedPrice;
            IsoCurrencyCode = isoCurrencyCode;
            IsRestore = isRestore;
            NativeOrder = nativeOrder;
        }
    }

    /// <summary>
    /// Seam for analytics + server-side receipt reporting. Called AFTER the purchase is applied
    /// and confirmed. Implementations must not throw (the service logs and continues if they do).
    /// </summary>
    public interface IPurchaseReporter
    {
        void OnPurchaseConfirmed(IapPurchaseInfo info);
        void OnPurchaseFailed(string productId, string reason);
    }
}
