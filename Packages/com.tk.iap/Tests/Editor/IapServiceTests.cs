using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.TestTools;

namespace TK.IAP.Tests
{
    [TestFixture]
    public sealed class IapServiceTests
    {
        // ── Helpers ──

        private static IapCatalog MakeCatalog(params IapCatalog.Entry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>(entries));
            return catalog;
        }

        private static IapCatalog.Entry Entry(string id, string storeId, ProductType productType, params IapCatalog.Item[] items)
            => new() { id = id, storeId = storeId, productType = productType, items = new List<IapCatalog.Item>(items) };

        private static IapCatalog.Item Item(string type, int amount = 1, string value = null)
            => new() { type = type, amount = amount, value = value };

        private static IapService NewService(IapCatalog catalog, FakeStoreGateway gateway, FakeSaveSystem save,
            bool isApple, IPurchaseReporter reporter = null, IIapAmountResolver resolver = null,
            int fetchAttempts = 3, TimeSpan? retryDelay = null)
        {
            return new IapService(catalog, save, new IapOptions
            {
                Gateway = gateway,
                Reporter = reporter,
                AmountResolver = resolver,
                ProductFetchAttempts = fetchAttempts,
                ProductFetchRetryDelay = retryDelay ?? TimeSpan.FromSeconds(2),
                IsApplePlatformOverride = isApple
            });
        }

        private sealed class RecordingReporter : IPurchaseReporter
        {
            public readonly List<IapPurchaseInfo> Confirmed = new();
            public readonly List<(string productId, string reason)> Failed = new();
            public Exception ThrowOnConfirmed;

            public void OnPurchaseConfirmed(IapPurchaseInfo info)
            {
                Confirmed.Add(info);
                if (ThrowOnConfirmed != null) throw ThrowOnConfirmed;
            }

            public void OnPurchaseFailed(string productId, string reason) => Failed.Add((productId, reason));
        }

        private sealed class FixedAmountResolver : IIapAmountResolver
        {
            public int Amount;
            public int Resolve(string productId, string itemType, int defaultAmount) => Amount;
        }

        // ── Initialization ──

        [Test]
        public void Initialize_HappyPath_FetchesProductsThenPurchases_StateInitialized()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);

            var initializedRaised = 0;
            var fetchPurchasesCallsAtInitialized = -1;
            svc.Initialized += () =>
            {
                initializedRaised++;
                fetchPurchasesCallsAtInitialized = gateway.FetchPurchasesCalls;
            };

            svc.InitializeAsync().Wait();

            Assert.AreEqual(IapInitState.Initialized, svc.State);
            Assert.AreEqual(1, initializedRaised);
            Assert.AreEqual(1, gateway.FetchProductsCalls);
            Assert.AreEqual(0, fetchPurchasesCallsAtInitialized, "Initialized must fire before the purchases fetch");
            Assert.AreEqual(1, gateway.FetchPurchasesCalls);
        }

        [Test]
        public void Initialize_ConnectFails_StateFailed_InitFailedRaised()
        {
            var gateway = new FakeStoreGateway { FailConnect = true };
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            var initFailedRaised = 0;
            svc.InitFailed += () => initFailedRaised++;

            LogAssert.Expect(LogType.Error, new Regex("Store connection failed"));
            svc.InitializeAsync().Wait();

            Assert.AreEqual(IapInitState.Failed, svc.State);
            Assert.AreEqual(1, initFailedRaised);
            Assert.AreEqual(0, gateway.FetchProductsCalls);
        }

        [Test]
        public async Task Initialize_ProductFetchFailsTwiceThenSucceeds_Recovers()
        {
            var gateway = new FakeStoreGateway { FailProductFetchTimes = 2 };
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false,
                fetchAttempts: 3, retryDelay: TimeSpan.Zero);

            await svc.InitializeAsync();
            for (int i = 0; i < 100 && svc.State == IapInitState.Initializing; i++) await Task.Yield();

            Assert.AreEqual(IapInitState.Initialized, svc.State);
            Assert.AreEqual(3, gateway.FetchProductsCalls);
            Assert.AreEqual(1, gateway.FetchPurchasesCalls);
        }

        [Test]
        public async Task Initialize_ProductFetchExhaustsRetries_StateFailed()
        {
            var gateway = new FakeStoreGateway { FailProductFetchTimes = 2 };
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false,
                fetchAttempts: 2, retryDelay: TimeSpan.Zero);
            var initFailedRaised = 0;
            svc.InitFailed += () => initFailedRaised++;

            await svc.InitializeAsync();
            for (int i = 0; i < 100 && svc.State == IapInitState.Initializing; i++) await Task.Yield();

            Assert.AreEqual(IapInitState.Failed, svc.State);
            Assert.AreEqual(1, initFailedRaised);
            Assert.AreEqual(2, gateway.FetchProductsCalls);
        }

        // ── Pending → apply → confirm ──

        [Test]
        public void Purchase_AppliesItemsViaHandler_ThenConfirms()
        {
            var gateway = new FakeStoreGateway();
            gateway.Products.Add(new StoreProduct("store.pack1", 4.99m, "USD", "$4.99", "Pack 1"));
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var reporter = new RecordingReporter();
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false, reporter: reporter);

            var handlerCalls = new List<(int amount, IapItemContext context)>();
            svc.RegisterItemHandler("coins", (amount, context) => handlerCalls.Add((amount, context)));
            svc.InitializeAsync().Wait();

            string purchasedId = null;
            svc.ProductPurchased += id => purchasedId = id;

            svc.Purchase("pack1");

            Assert.AreEqual(1, handlerCalls.Count);
            Assert.AreEqual(300, handlerCalls[0].amount);
            Assert.AreEqual("pack1", handlerCalls[0].context.ProductId);
            Assert.AreEqual("coins", handlerCalls[0].context.ItemType);
            Assert.IsFalse(handlerCalls[0].context.IsRestore);
            Assert.AreEqual(1, gateway.ConfirmCalls.Count);
            Assert.AreEqual("pack1", purchasedId);

            Assert.AreEqual(1, reporter.Confirmed.Count);
            var info = reporter.Confirmed[0];
            Assert.AreEqual("pack1", info.ProductId);
            Assert.AreEqual("store.pack1", info.StoreId);
            Assert.AreEqual("tx_1", info.TransactionId);
            Assert.AreEqual(4.99m, info.LocalizedPrice);
            Assert.AreEqual("USD", info.IsoCurrencyCode);
            Assert.IsFalse(info.IsRestore);
        }

        [Test]
        public void Purchase_HandlerMissing_DoesNotConfirm_StaysPending()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            svc.InitializeAsync().Wait();

            var purchasedRaised = false;
            svc.ProductPurchased += _ => purchasedRaised = true;

            LogAssert.Expect(LogType.Error, new Regex("No handler for item type 'coins'"));
            svc.Purchase("pack1");

            Assert.AreEqual(0, gateway.ConfirmCalls.Count, "purchase must never be confirmed when applying failed");
            Assert.IsFalse(purchasedRaised);
        }

        [Test]
        public void Purchase_HandlerThrows_DoesNotConfirm()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            svc.RegisterItemHandler("coins", (_, _) => throw new InvalidOperationException("handler boom"));
            svc.InitializeAsync().Wait();

            var purchasedRaised = false;
            svc.ProductPurchased += _ => purchasedRaised = true;

            LogAssert.Expect(LogType.Exception, new Regex("handler boom"));
            svc.Purchase("pack1");

            Assert.AreEqual(0, gateway.ConfirmCalls.Count, "purchase must never be confirmed when the handler threw");
            Assert.IsFalse(purchasedRaised);
        }

        [Test]
        public void PendingRedelivery_AfterApplyButBeforeConfirmCrash_ConfirmsWithoutReapplying()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            var handlerCalls = 0;
            svc.RegisterItemHandler("coins", (_, _) => handlerCalls++);
            svc.InitializeAsync().Wait();

            gateway.DeliverPending("store.pack1", "tx_fixed");

            Assert.AreEqual(1, handlerCalls);
            Assert.AreEqual(1, gateway.ConfirmCalls.Count);

            // Store re-delivers the same transaction (crash after apply, before the confirm reached the store).
            gateway.DeliverPending("store.pack1", "tx_fixed");

            Assert.AreEqual(1, handlerCalls, "re-delivered transaction must not re-apply items");
            Assert.AreEqual(2, gateway.ConfirmCalls.Count, "re-delivered transaction must still be confirmed");
        }

        // ── Non-consumables + entitlements ──

        [Test]
        public void NonConsumablePurchase_GrantsEntitlementByCatalogId()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("remove_ads", "store.remove_ads", ProductType.NonConsumable));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            svc.InitializeAsync().Wait();

            svc.Purchase("remove_ads");

            Assert.IsTrue(svc.Entitlements.Has("remove_ads"));
            Assert.IsTrue(svc.OwnsNonConsumable("remove_ads"));
            Assert.AreEqual(1, gateway.ConfirmCalls.Count);
        }

        [Test]
        public void EntitlementItem_InConsumableBundle_GrantsKey()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack10", "store.pack10", ProductType.Consumable,
                Item("coins", 300), Item(IapService.EntitlementItemType, 1, "remove_ads")));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            var coinsGranted = 0;
            svc.RegisterItemHandler("coins", (amount, _) => coinsGranted += amount);
            svc.InitializeAsync().Wait();

            svc.Purchase("pack10");

            Assert.AreEqual(300, coinsGranted);
            Assert.IsTrue(svc.Entitlements.Has("remove_ads"));
            Assert.AreEqual(1, gateway.ConfirmCalls.Count);
        }

        // ── History / restore ──

        [Test]
        public void History_OnGoogle_AutoAppliesUnownedNonConsumables_Once()
        {
            var save = new FakeSaveSystem();
            var catalog = MakeCatalog(Entry("remove_ads", "store.remove_ads", ProductType.NonConsumable, Item("premium", 1)));

            var gateway1 = new FakeStoreGateway();
            gateway1.PurchaseHistory.Add(new ConfirmedPurchase("store.remove_ads", "tx_hist_1"));
            var svc1 = NewService(catalog, gateway1, save, isApple: false);
            var contexts1 = new List<IapItemContext>();
            svc1.RegisterItemHandler("premium", (_, context) => contexts1.Add(context));

            svc1.InitializeAsync().Wait();

            Assert.AreEqual(1, contexts1.Count);
            Assert.IsTrue(contexts1[0].IsRestore);
            Assert.IsTrue(svc1.Entitlements.Has("remove_ads"));
            Assert.IsTrue(svc1.OwnsNonConsumable("remove_ads"));

            // A new session over the same save must not re-apply the already-granted product.
            var gateway2 = new FakeStoreGateway();
            gateway2.PurchaseHistory.Add(new ConfirmedPurchase("store.remove_ads", "tx_hist_1"));
            var svc2 = NewService(catalog, gateway2, save, isApple: false);
            var handlerCalls2 = 0;
            svc2.RegisterItemHandler("premium", (_, _) => handlerCalls2++);

            svc2.InitializeAsync().Wait();

            Assert.AreEqual(0, handlerCalls2, "ledger must prevent double-applying a granted non-consumable");
            Assert.IsTrue(svc2.Entitlements.Has("remove_ads"));
            Assert.IsTrue(svc2.OwnsNonConsumable("remove_ads"));
        }

        [Test]
        public void History_OnApple_NotAppliedWithoutRestoreRequest()
        {
            var gateway = new FakeStoreGateway();
            gateway.PurchaseHistory.Add(new ConfirmedPurchase("store.remove_ads", "tx_hist_1"));
            var catalog = MakeCatalog(Entry("remove_ads", "store.remove_ads", ProductType.NonConsumable));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: true);
            bool? restoreResult = null;
            svc.RestoreCompleted += ok => restoreResult = ok;

            svc.InitializeAsync().Wait();

            Assert.IsFalse(svc.Entitlements.Has("remove_ads"), "Apple must not apply history without an explicit restore");
            Assert.IsNull(restoreResult);

            svc.RestorePurchases();

            Assert.IsTrue(svc.Entitlements.Has("remove_ads"));
            Assert.AreEqual(true, restoreResult);
            Assert.AreEqual(2, gateway.FetchPurchasesCalls);
        }

        [Test]
        public void Restore_OnGoogle_IsNoOp_CompletedTrue()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            svc.InitializeAsync().Wait();
            bool? restoreResult = null;
            svc.RestoreCompleted += ok => restoreResult = ok;

            svc.RestorePurchases();

            Assert.AreEqual(true, restoreResult);
            Assert.AreEqual(1, gateway.FetchPurchasesCalls, "Google restore must not trigger another purchases fetch");
        }

        // ── Failure + query paths ──

        [Test]
        public void Purchase_UnknownProduct_FailsWithUnknownProduct()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            svc.InitializeAsync().Wait();
            var failures = new List<(string productId, string reason)>();
            svc.PurchaseFailed += (id, reason) => failures.Add((id, reason));

            LogAssert.Expect(LogType.Error, new Regex("No entry for id 'nope'"));
            svc.Purchase("nope");

            Assert.AreEqual(1, failures.Count);
            Assert.AreEqual("nope", failures[0].productId);
            Assert.AreEqual("unknown_product", failures[0].reason);
            Assert.AreEqual(0, gateway.PurchaseCalls.Count);
        }

        [Test]
        public void AmountResolver_OverridesCatalogAmount_ForApplyAndQuery()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var resolver = new FixedAmountResolver { Amount = 999 };
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false, resolver: resolver);
            var receivedAmount = -1;
            svc.RegisterItemHandler("coins", (amount, _) => receivedAmount = amount);
            svc.InitializeAsync().Wait();

            svc.Purchase("pack1");

            Assert.AreEqual(999, receivedAmount, "apply path must use the resolver amount");
            Assert.AreEqual(999, svc.GetItemAmount("pack1", "coins"), "query path must use the resolver amount");
        }

        [Test]
        public void Reporter_Throwing_DoesNotBreakPurchase()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var reporter = new RecordingReporter { ThrowOnConfirmed = new InvalidOperationException("reporter boom") };
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false, reporter: reporter);
            var handlerCalls = 0;
            svc.RegisterItemHandler("coins", (_, _) => handlerCalls++);
            svc.InitializeAsync().Wait();
            string purchasedId = null;
            svc.ProductPurchased += id => purchasedId = id;

            LogAssert.Expect(LogType.Exception, new Regex("reporter boom"));
            svc.Purchase("pack1");

            Assert.AreEqual(1, handlerCalls);
            Assert.AreEqual(1, gateway.ConfirmCalls.Count);
            Assert.AreEqual("pack1", purchasedId);
            Assert.AreEqual(1, reporter.Confirmed.Count);
        }

        // ── Rider: purchases-fetch failure + init robustness ──

        [Test]
        public void Restore_FetchFails_CompletesFalse_AndResetsLatch()
        {
            var gateway = new FakeStoreGateway();
            var catalog = MakeCatalog(Entry("remove_ads", "store.remove_ads", ProductType.NonConsumable));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: true);
            svc.InitializeAsync().Wait();

            var restoreCalls = 0;
            bool? lastResult = null;
            svc.RestoreCompleted += ok => { restoreCalls++; lastResult = ok; };

            gateway.FailNextFetchPurchases = true;
            LogAssert.Expect(LogType.Warning, new Regex("Purchases fetch failed"));
            svc.RestorePurchases();

            Assert.AreEqual(1, restoreCalls);
            Assert.AreEqual(false, lastResult);

            // Unrelated later fetch delivering successfully must not re-trigger RestoreCompleted (latch reset).
            gateway.FetchPurchases();

            Assert.AreEqual(1, restoreCalls, "latch must be reset so a stale RestoreCompleted(true) never fires");
        }

        [Test]
        public void Initialize_ConnectThrows_StateFailed()
        {
            var gateway = new FakeStoreGateway { ThrowOnConnect = true };
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false);
            var initFailedRaised = 0;
            svc.InitFailed += () => initFailedRaised++;

            LogAssert.Expect(LogType.Exception, new Regex("connect exploded"));
            svc.InitializeAsync().Wait();

            Assert.AreEqual(IapInitState.Failed, svc.State);
            Assert.AreEqual(1, initFailedRaised);
        }

        [Test]
        public void LateProductsFetched_AfterFailed_DoesNotInitialize()
        {
            var gateway = new FakeStoreGateway { FailProductFetchTimes = 1 };
            var catalog = MakeCatalog(Entry("pack1", "store.pack1", ProductType.Consumable, Item("coins", 300)));
            var svc = NewService(catalog, gateway, new FakeSaveSystem(), isApple: false, fetchAttempts: 1);
            var initializedRaised = 0;
            svc.Initialized += () => initializedRaised++;

            svc.InitializeAsync().Wait();

            Assert.AreEqual(IapInitState.Failed, svc.State);

            gateway.DeliverProductsFetched();

            Assert.AreEqual(IapInitState.Failed, svc.State, "a late products-fetched race must not resurrect a failed init");
            Assert.AreEqual(0, initializedRaised);
        }
    }
}
