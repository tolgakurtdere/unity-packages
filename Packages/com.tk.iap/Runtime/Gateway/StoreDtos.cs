using System;

namespace TK.IAP
{
    /// <summary>What to fetch from the store: platform store id + Unity product type.</summary>
    public readonly struct StoreProductDefinition
    {
        public string StoreId { get; }
        public UnityEngine.Purchasing.ProductType ProductType { get; }

        public StoreProductDefinition(string storeId, UnityEngine.Purchasing.ProductType productType)
        {
            StoreId = storeId;
            ProductType = productType;
        }
    }

    /// <summary>Store-side product data after a successful fetch.</summary>
    public readonly struct StoreProduct
    {
        public string StoreId { get; }
        public decimal LocalizedPrice { get; }
        public string IsoCurrencyCode { get; }
        public string LocalizedPriceString { get; }
        public string LocalizedTitle { get; }

        public StoreProduct(string storeId, decimal localizedPrice, string isoCurrencyCode, string localizedPriceString, string localizedTitle)
        {
            StoreId = storeId;
            LocalizedPrice = localizedPrice;
            IsoCurrencyCode = isoCurrencyCode;
            LocalizedPriceString = localizedPriceString;
            LocalizedTitle = localizedTitle;
        }
    }

    /// <summary>A purchase delivered by the store, awaiting application + confirmation.</summary>
    public sealed class PendingPurchase
    {
        public string StoreId { get; }
        public string TransactionId { get; }
        /// <summary>Backend-native object (Unity IAP PendingOrder). Null in fakes.</summary>
        public object Native { get; }

        public PendingPurchase(string storeId, string transactionId, object native = null)
        {
            StoreId = storeId;
            TransactionId = transactionId;
            Native = native;
        }
    }

    /// <summary>A purchase the store considers finalized (confirmed this session or fetched from history).</summary>
    public sealed class ConfirmedPurchase
    {
        public string StoreId { get; }
        public string TransactionId { get; }
        public object Native { get; }

        public ConfirmedPurchase(string storeId, string transactionId, object native = null)
        {
            StoreId = storeId;
            TransactionId = transactionId;
            Native = native;
        }
    }

    public sealed class FailedPurchase
    {
        public string StoreId { get; }
        public string Reason { get; }

        public FailedPurchase(string storeId, string reason)
        {
            StoreId = storeId;
            Reason = reason;
        }
    }
}
