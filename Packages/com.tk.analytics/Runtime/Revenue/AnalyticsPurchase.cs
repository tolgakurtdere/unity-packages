namespace TK.Analytics
{
    /// <summary>Neutral, package-owned purchase record. Bridges map com.tk.iap's IapPurchaseInfo into this.</summary>
    public readonly struct AnalyticsPurchase
    {
        public string ProductId { get; }
        public double Price { get; }          // localized price as double
        public string Currency { get; }       // ISO 4217; may be null
        public string TransactionId { get; }
        public int    Quantity { get; }       // defaults to 1
        public bool   IsRestore { get; }

        public AnalyticsPurchase(string productId, double price, string currency,
            string transactionId, int quantity = 1, bool isRestore = false)
        {
            ProductId = productId;
            Price = price;
            Currency = currency;
            TransactionId = transactionId;
            Quantity = quantity;
            IsRestore = isRestore;
        }
    }
}
