using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.Core.Save;
using UnityEngine;
using UnityEngine.Purchasing;

namespace TK.IAP
{
    public delegate void IapItemHandler(int amount, IapItemContext context);

    public readonly struct IapItemContext
    {
        public string ProductId { get; }
        public string ItemType { get; }
        public string Value { get; }
        public bool IsRestore { get; }

        public IapItemContext(string productId, string itemType, string value, bool isRestore)
        {
            ProductId = productId;
            ItemType = itemType;
            Value = value;
            IsRestore = isRestore;
        }
    }

    public readonly struct IapPrice
    {
        public decimal Amount { get; }
        public string IsoCurrencyCode { get; }
        public string Display { get; }

        public IapPrice(decimal amount, string isoCurrencyCode, string display)
        {
            Amount = amount;
            IsoCurrencyCode = isoCurrencyCode;
            Display = display;
        }
    }

    public sealed class IapOptions
    {
        /// <summary>Store backend override. Null = UnityIapGateway (the real store).</summary>
        public IStoreGateway Gateway;
        public IPurchaseReporter Reporter;
        public IIapAmountResolver AmountResolver;
        /// <summary>How many product-fetch attempts before init is declared Failed.</summary>
        public int ProductFetchAttempts = 3;
        /// <summary>Base delay between product-fetch retries; doubles per attempt.</summary>
        public TimeSpan ProductFetchRetryDelay = TimeSpan.FromSeconds(2);
        /// <summary>Treat the current platform as Apple (restore-button flow). Null = auto-detect.</summary>
        public bool? IsApplePlatformOverride;
    }

    /// <summary>
    /// Main IAP facade. Owns the store lifecycle (connect → fetch products with retry → fetch
    /// purchases), the pending→apply→confirm contract (a purchase is NEVER confirmed to the
    /// store unless applying it to the game succeeded), restore flows, and idempotency.
    /// Item meaning is the game's: register handlers per item type before InitializeAsync.
    /// Main-thread only.
    /// One service per gateway instance: to retry after a Failed init, construct a fresh service
    /// AND a fresh gateway (event subscriptions are never removed).
    /// </summary>
    public sealed class IapService
    {
        /// <summary>Set by InitializeAsync for scene-component access. Prefer injection where possible.</summary>
        public static IapService Instance { get; private set; }

        public const string EntitlementItemType = "entitlement";

        public IapInitState State { get; private set; } = IapInitState.NotInitialized;
        public IapCatalog Catalog { get; }
        public Entitlements Entitlements { get; }

        public event Action Initialized;
        public event Action InitFailed;
        /// <summary>Raised after a purchase is applied AND confirmed. Parameter: catalog id.</summary>
        public event Action<string> ProductPurchased;
        /// <summary>Parameters: catalog id (or store id if unknown), failure reason.</summary>
        public event Action<string, string> PurchaseFailed;
        public event Action<bool> RestoreCompleted;

        private readonly ISaveSystem _save;
        private readonly IStoreGateway _gateway;
        private readonly IPurchaseReporter _reporter;
        private readonly IIapAmountResolver _amountResolver;
        private readonly IapOptions _options;
        private readonly AppliedPurchaseLedger _ledger;
        private readonly Dictionary<string, IapItemHandler> _itemHandlers = new();
        private readonly bool _isApplePlatform;

        private bool _restoreRequested;   // Apple: user tapped Restore; gates history application
        private int _productFetchAttemptsLeft;
        private Task _initTask;

        public IapService(IapCatalog catalog, ISaveSystem save, IapOptions options = null)
        {
            Catalog = catalog ? catalog : throw new ArgumentNullException(nameof(catalog));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _options = options ?? new IapOptions();
            _gateway = _options.Gateway ?? CreateDefaultGateway();
            _reporter = _options.Reporter;
            _amountResolver = _options.AmountResolver;
            _isApplePlatform = _options.IsApplePlatformOverride
                               ?? Application.platform is RuntimePlatform.IPhonePlayer or RuntimePlatform.OSXPlayer;
            // covers iOS/macOS; set IsApplePlatformOverride explicitly for tvOS/visionOS (and always in tests)

            Entitlements = new Entitlements(_save);
            _ledger = new AppliedPurchaseLedger(_save);

            Instance = this;
        }

        private static IStoreGateway CreateDefaultGateway()
        {
            return new UnityIapGateway();
        }

        /// <summary>Register a handler for an item type BEFORE InitializeAsync (init may re-deliver pending orders).</summary>
        public void RegisterItemHandler(string itemType, IapItemHandler handler)
        {
            if (string.IsNullOrEmpty(itemType) || handler == null)
            {
                Debug.LogError("[IapService] RegisterItemHandler: itemType and handler are required.");
                return;
            }

            if (!_itemHandlers.TryAdd(itemType, handler))
                Debug.LogError($"[IapService] Item handler for '{itemType}' is already registered.");
        }

        public async Task InitializeAsync()
        {
            if (_initTask != null)
            {
                await _initTask;
                return;
            }

            _initTask = InitializeInternalAsync();
            await _initTask;
        }

        private async Task InitializeInternalAsync()
        {
            State = IapInitState.Initializing;
            _productFetchAttemptsLeft = Mathf.Max(1, _options.ProductFetchAttempts);

            _gateway.Connected += OnConnected;
            _gateway.ConnectionFailed += OnConnectionFailed;
            _gateway.ProductsFetched += OnProductsFetched;
            _gateway.ProductsFetchFailed += OnProductsFetchFailed;
            _gateway.PurchasePending += OnPurchasePending;
            _gateway.PurchaseFailed += OnPurchaseFailed;
            _gateway.PurchasesFetched += OnPurchasesFetched;
            _gateway.PurchasesFetchFailed += OnPurchasesFetchFailed;

            try
            {
                await _gateway.ConnectAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                State = IapInitState.Failed;
                InitFailed?.Invoke();
            }
        }

        private void OnPurchasesFetchFailed(string message)
        {
            Debug.LogWarning($"[IapService] Purchases fetch failed: {message}");
            if (_restoreRequested)
            {
                _restoreRequested = false;
                RestoreCompleted?.Invoke(false);
            }
        }

        private void OnConnected()
        {
            _gateway.FetchProducts(Catalog.BuildDefinitions(_isApplePlatform));
        }

        private void OnConnectionFailed(string message)
        {
            Debug.LogError($"[IapService] Store connection failed: {message}");
            State = IapInitState.Failed;
            InitFailed?.Invoke();
        }

        private void OnProductsFetched(IReadOnlyList<StoreProduct> products)
        {
            if (State == IapInitState.Initializing)
            {
                State = IapInitState.Initialized;
                Initialized?.Invoke();
                // Non-consumable ownership + pending crash-recovery orders arrive via history.
                _gateway.FetchPurchases();
            }
        }

        private async void OnProductsFetchFailed(string message)
        {
            _productFetchAttemptsLeft--;
            Debug.LogWarning($"[IapService] Products fetch failed ({message}); attempts left: {_productFetchAttemptsLeft}");

            if (_productFetchAttemptsLeft <= 0)
            {
                State = IapInitState.Failed;
                InitFailed?.Invoke();
                return;
            }

            try
            {
                var attemptIndex = Mathf.Clamp(_options.ProductFetchAttempts - 1 - _productFetchAttemptsLeft, 0, 8);
                var delay = TimeSpan.FromTicks(_options.ProductFetchRetryDelay.Ticks << attemptIndex);
                await Task.Delay(delay);
                _gateway.FetchProducts(Catalog.BuildDefinitions(_isApplePlatform));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                State = IapInitState.Failed;
                InitFailed?.Invoke();
            }
        }

        /// <summary>Starts a purchase by CATALOG id.</summary>
        public void Purchase(string productId)
        {
            if (State != IapInitState.Initialized)
            {
                Debug.LogError($"[IapService] Purchase('{productId}') ignored — service not initialized (state: {State}).");
                PurchaseFailed?.Invoke(productId, "not_initialized");
                return;
            }

            var entry = Catalog.Get(productId);
            if (entry == null)
            {
                PurchaseFailed?.Invoke(productId, "unknown_product");
                return;
            }

            _gateway.Purchase(entry.storeId);
        }

        /// <summary>
        /// Apple: applies purchase history on the user's explicit request (App Review requirement);
        /// Google restores automatically on init, so this is a logged no-op there.
        /// </summary>
        public void RestorePurchases()
        {
            if (State != IapInitState.Initialized)
            {
                Debug.LogWarning("[IapService] RestorePurchases ignored — not initialized.");
                RestoreCompleted?.Invoke(false);
                return;
            }

            if (!_isApplePlatform)
            {
                Debug.Log("[IapService] RestorePurchases: Google Play restores automatically; nothing to do.");
                RestoreCompleted?.Invoke(true);
                return;
            }

            _restoreRequested = true;
            _gateway.FetchPurchases();
        }

        // ── Pending → apply → confirm (the core contract) ──

        private void OnPurchasePending(PendingPurchase pending)
        {
            var entry = FindEntryByStoreId(pending.StoreId);
            if (entry == null)
            {
                Debug.LogError($"[IapService] Pending purchase for unknown storeId '{pending.StoreId}' — leaving unconfirmed.");
                return;
            }

            // Crash recovery: applied but the confirm never reached the store → just confirm.
            if (_ledger.IsTransactionApplied(pending.TransactionId))
            {
                Debug.Log($"[IapService] Transaction '{pending.TransactionId}' already applied — confirming only.");
                _gateway.Confirm(pending);
                FinishConfirmed(entry, pending.StoreId, pending.TransactionId, isRestore: false, native: pending.Native);
                return;
            }

            if (!TryApplyItems(entry, isRestore: false)) return; // stays pending; store re-delivers next session

            _ledger.MarkTransactionApplied(pending.TransactionId);
            if (entry.productType == ProductType.NonConsumable)
                MarkNonConsumableOwned(entry);

            _gateway.Confirm(pending);
            FinishConfirmed(entry, pending.StoreId, pending.TransactionId, isRestore: false, native: pending.Native);
        }

        private void OnPurchaseFailed(FailedPurchase failed)
        {
            var entry = FindEntryByStoreId(failed.StoreId);
            var id = entry?.id ?? failed.StoreId;
            Debug.LogWarning($"[IapService] Purchase failed for '{id}': {failed.Reason}");
            PurchaseFailed?.Invoke(id, failed.Reason);
            SafeReport(r => r.OnPurchaseFailed(id, failed.Reason));
        }

        // ── History (auto-restore on Google / requested restore on Apple / ownership sync) ──

        private void OnPurchasesFetched(IReadOnlyList<ConfirmedPurchase> history)
        {
            // Apple applies history only on explicit user request (mirrors the reference iOS guard).
            var applyHistory = !_isApplePlatform || _restoreRequested;
            var wasExplicitRestore = _restoreRequested;
            _restoreRequested = false;

            if (!applyHistory) return;

            foreach (var confirmed in history)
            {
                var entry = FindEntryByStoreId(confirmed.StoreId);
                if (entry == null || entry.productType != ProductType.NonConsumable) continue;
                if (_ledger.IsProductApplied(entry.id)) continue;   // already granted — never double-apply

                if (TryApplyItems(entry, isRestore: true))
                {
                    MarkNonConsumableOwned(entry);
                    FinishConfirmed(entry, confirmed.StoreId, confirmed.TransactionId, isRestore: true, native: confirmed.Native);
                }
            }

            if (wasExplicitRestore) RestoreCompleted?.Invoke(true);
        }

        private void MarkNonConsumableOwned(IapCatalog.Entry entry)
        {
            _ledger.MarkProductApplied(entry.id);
            // Non-consumables double as entitlements keyed by catalog id ("remove_ads" etc.).
            Entitlements.Grant(entry.id);
        }

        // ── Item application ──

        private bool TryApplyItems(IapCatalog.Entry entry, bool isRestore)
        {
            try
            {
                foreach (var item in entry.items)
                {
                    var amount = GetItemAmount(entry.id, item);

                    if (item.type == EntitlementItemType)
                    {
                        if (string.IsNullOrEmpty(item.value))
                            Debug.LogError($"[IapService] '{entry.id}': entitlement item without a key in 'value'.");
                        else
                            Entitlements.Grant(item.value);
                        continue;
                    }

                    if (_itemHandlers.TryGetValue(item.type, out var handler))
                    {
                        handler(amount, new IapItemContext(entry.id, item.type, item.value, isRestore));
                    }
                    else
                    {
                        Debug.LogError($"[IapService] No handler for item type '{item.type}' (product '{entry.id}'). Purchase left unconfirmed.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private void FinishConfirmed(IapCatalog.Entry entry, string storeId, string transactionId, bool isRestore, object native)
        {
            ProductPurchased?.Invoke(entry.id);

            decimal price = 0m;
            string currency = null;
            if (_gateway.TryGetProduct(storeId, out var product))
            {
                price = product.LocalizedPrice;
                currency = product.IsoCurrencyCode;
            }

            var info = new IapPurchaseInfo(entry.id, storeId, transactionId, price, currency, isRestore, native);
            SafeReport(r => r.OnPurchaseConfirmed(info));
        }

        private void SafeReport(Action<IPurchaseReporter> action)
        {
            if (_reporter == null) return;
            try { action(_reporter); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        // ── Queries (UI) ──

        public bool TryGetPrice(string productId, out IapPrice price)
        {
            price = default;
            if (!Catalog.TryGet(productId, out var entry)) return false;
            if (!_gateway.TryGetProduct(entry.storeId, out var product)) return false;

            price = new IapPrice(product.LocalizedPrice, product.IsoCurrencyCode, product.LocalizedPriceString);
            return true;
        }

        /// <summary>Resolver-aware item amount for UI display. Returns 0 if product/item missing.</summary>
        public int GetItemAmount(string productId, string itemType)
        {
            if (!Catalog.TryGet(productId, out var entry)) return 0;

            foreach (var item in entry.items)
            {
                if (item.type == itemType) return GetItemAmount(productId, item);
            }

            return 0;
        }

        private int GetItemAmount(string productId, IapCatalog.Item item)
            => _amountResolver?.Resolve(productId, item.type, item.amount) ?? item.amount;

        public bool OwnsNonConsumable(string productId) => _ledger.IsProductApplied(productId);

        private IapCatalog.Entry FindEntryByStoreId(string storeId)
        {
            foreach (var entry in Catalog.Entries)
            {
                if (entry.storeId == storeId) return entry;
            }

            return null;
        }
    }
}
