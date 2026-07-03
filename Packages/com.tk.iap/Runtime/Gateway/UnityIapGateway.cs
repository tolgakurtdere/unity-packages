using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace TK.IAP
{
    /// <summary>
    /// Unity IAP (com.unity.purchasing v5) backed store gateway — the default IStoreGateway used
    /// when IapOptions.Gateway is null. Thin adapter: owns one StoreController, adapts its v5
    /// events to the package's store DTOs, and never applies game logic itself (that's IapService).
    /// In the Unity Editor, purchasing v5 runs against Unity's built-in fake store (purchases
    /// auto-succeed) — this is the path used by the Samples~ demo scene.
    /// </summary>
    public sealed class UnityIapGateway : IStoreGateway
    {
        public event Action Connected;
        public event Action<string> ConnectionFailed;
        public event Action<IReadOnlyList<StoreProduct>> ProductsFetched;
        public event Action<string> ProductsFetchFailed;
        public event Action<PendingPurchase> PurchasePending;
        public event Action<ConfirmedPurchase> PurchaseConfirmed;
        public event Action<FailedPurchase> PurchaseFailed;
        public event Action<IReadOnlyList<ConfirmedPurchase>> PurchasesFetched;
        public event Action<string> PurchasesFetchFailed;

        private StoreController _controller;
        private bool _connectFailed;

        public Task ConnectAsync()
        {
            _controller = UnityIAPServices.StoreController();

            _controller.ProcessPendingOrdersOnPurchasesFetched(false);
            _controller.SetStoreReconnectionRetryPolicyOnDisconnection(new ExponentialBackOffRetryPolicy(1000, 60000, 2F));

            // Subscribe before Connect() so no early callback (e.g. an immediate disconnect) is missed.
            _controller.OnStoreDisconnected += OnStoreDisconnected;
            _controller.OnProductsFetched += OnProductsFetchedInternal;
            _controller.OnProductsFetchFailed += OnProductsFetchFailedInternal;
            _controller.OnPurchasePending += OnPurchasePendingInternal;
            _controller.OnPurchaseFailed += OnPurchaseFailedInternal;
            _controller.OnPurchaseConfirmed += OnPurchaseConfirmedInternal;
            _controller.OnPurchasesFetched += OnPurchasesFetchedInternal;
            _controller.OnPurchasesFetchFailed += OnPurchasesFetchFailedInternal;

            return ConnectCoreAsync();
        }

        private async Task ConnectCoreAsync()
        {
            // Connect()'s Task resolves on BOTH success and exhausted-retry failure (v5 never
            // faults it); OnStoreDisconnected is the only failure signal, so a latch distinguishes
            // the two outcomes rather than always raising Connected once the await completes.
            _connectFailed = false;
            await _controller.Connect();
            if (!_connectFailed)
                Connected?.Invoke();
        }

        public void FetchProducts(IReadOnlyList<StoreProductDefinition> definitions)
        {
            var productDefinitions = new List<ProductDefinition>(definitions.Count);
            foreach (var definition in definitions)
                productDefinitions.Add(new ProductDefinition(definition.StoreId, definition.ProductType));

            _controller.FetchProducts(productDefinitions);
        }

        public void FetchPurchases() => _controller.FetchPurchases();

        public void Purchase(string storeId) => _controller.PurchaseProduct(storeId);

        public void Confirm(PendingPurchase pending)
        {
            if (pending.Native is PendingOrder nativeOrder)
            {
                _controller.ConfirmPurchase(nativeOrder);
            }
            else
            {
                Debug.LogError($"[UnityIapGateway] Confirm('{pending.StoreId}') — pending.Native is not a PendingOrder (was: {pending.Native?.GetType().Name ?? "null"}). Purchase left unconfirmed.");
            }
        }

        public bool TryGetProduct(string storeId, out StoreProduct product)
        {
            var native = _controller.GetProductById(storeId);
            if (native == null)
            {
                product = default;
                return false;
            }

            product = ToStoreProduct(native);
            return true;
        }

        // ── v5 event adaptation ──

        private void OnStoreDisconnected(StoreConnectionFailureDescription error)
        {
            _connectFailed = true;
            ConnectionFailed?.Invoke(error.message);
        }

        private void OnProductsFetchedInternal(List<Product> products)
        {
            var mapped = new List<StoreProduct>(products.Count);
            foreach (var product in products)
                mapped.Add(ToStoreProduct(product));

            ProductsFetched?.Invoke(mapped);
        }

        private void OnProductsFetchFailedInternal(ProductFetchFailed error)
            => ProductsFetchFailed?.Invoke(error.FailureReason);

        private void OnPurchasePendingInternal(PendingOrder order)
            => PurchasePending?.Invoke(new PendingPurchase(FirstProductId(order), order.Info.TransactionID, order));

        private void OnPurchaseFailedInternal(FailedOrder order)
            => PurchaseFailed?.Invoke(new FailedPurchase(FirstProductId(order), order.FailureReason.ToString()));

        private void OnPurchaseConfirmedInternal(Order order)
            => PurchaseConfirmed?.Invoke(new ConfirmedPurchase(FirstProductId(order), order.Info.TransactionID, order));

        private void OnPurchasesFetchedInternal(Orders orders)
        {
            var mapped = new List<ConfirmedPurchase>(orders.ConfirmedOrders.Count);
            foreach (var order in orders.ConfirmedOrders)
                mapped.Add(new ConfirmedPurchase(FirstProductId(order), order.Info.TransactionID, order));

            PurchasesFetched?.Invoke(mapped);
        }

        private void OnPurchasesFetchFailedInternal(PurchasesFetchFailureDescription error)
            => PurchasesFetchFailed?.Invoke(error.message);

        // ── Helpers ──

        /// <summary>
        /// Best-effort store id for an order. Never throws (the reference implementation crashes
        /// on an empty PurchasedProductInfo list; we fall back instead): first PurchasedProductInfo
        /// entry, then the first cart item's product id, then "".
        /// </summary>
        private static string FirstProductId(Order order)
        {
            var purchasedProductInfo = order.Info?.PurchasedProductInfo;
            if (purchasedProductInfo is { Count: > 0 })
                return purchasedProductInfo[0].productId ?? "";

            return order.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id ?? "";
        }

        private static StoreProduct ToStoreProduct(Product product)
        {
            var metadata = product.metadata;
            if (metadata == null)
                return new StoreProduct(product.definition.id, 0m, "", "", "");

            return new StoreProduct(product.definition.id, metadata.localizedPrice, metadata.isoCurrencyCode ?? "",
                metadata.localizedPriceString ?? "", metadata.localizedTitle ?? "");
        }
    }
}
