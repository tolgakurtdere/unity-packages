using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TK.IAP;

namespace TK.IAP.Tests
{
    public sealed class FakeStoreGateway : IStoreGateway
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

        // ── Scripting knobs ──
        public bool FailConnect;
        public bool ThrowOnConnect;
        public int FailProductFetchTimes;                 // fail the first N FetchProducts calls
        public bool FailNextFetchPurchases;               // fail the next FetchPurchases call, then reset
        public readonly List<StoreProduct> Products = new();
        public readonly List<ConfirmedPurchase> PurchaseHistory = new();
        /// <summary>
        /// Pending orders a real Google store would re-deliver on the next FetchPurchases (crash
        /// recovery: purchased-but-unconfirmed from a previous session). Drained once per fetch —
        /// mirrors v5's per-session dedupe (PurchaseService.m_PurchasesProcessedInSession), where a
        /// FakeStoreGateway instance stands in for one "session".
        /// </summary>
        public readonly List<PendingPurchase> PendingHistory = new();
        public bool AutoDeliverPendingOnPurchase = true;  // Purchase(id) → immediate PurchasePending
        public int NextTransactionNumber = 1;

        // ── Recorded calls ──
        public readonly List<string> PurchaseCalls = new();
        public readonly List<PendingPurchase> ConfirmCalls = new();
        public int FetchPurchasesCalls;
        public int FetchProductsCalls;

        public Task ConnectAsync()
        {
            if (ThrowOnConnect) throw new InvalidOperationException("fake: connect exploded");
            if (FailConnect) ConnectionFailed?.Invoke("fake: connect failed");
            else Connected?.Invoke();
            return Task.CompletedTask;
        }

        public void FetchProducts(IReadOnlyList<StoreProductDefinition> definitions)
        {
            FetchProductsCalls++;
            if (FailProductFetchTimes > 0)
            {
                FailProductFetchTimes--;
                ProductsFetchFailed?.Invoke("fake: products fetch failed");
                return;
            }

            ProductsFetched?.Invoke(Products.Where(p => definitions.Any(d => d.StoreId == p.StoreId)).ToList());
        }

        public void FetchPurchases()
        {
            FetchPurchasesCalls++;
            if (FailNextFetchPurchases)
            {
                FailNextFetchPurchases = false;
                PurchasesFetchFailed?.Invoke("fake: purchases fetch failed");
                return;
            }

            // Real v5 (PurchaseService.OnFetchSuccess) raises OnPurchasePending for each fetched
            // pending order BEFORE OnPurchasesFetched — mirror that order here. Drain-then-clear
            // models v5's per-session dedupe: each pending is redelivered once per FakeStoreGateway
            // instance, not once per FetchPurchases call within the same instance.
            if (PendingHistory.Count > 0)
            {
                var toDeliver = new List<PendingPurchase>(PendingHistory);
                PendingHistory.Clear();
                foreach (var pending in toDeliver)
                    PurchasePending?.Invoke(pending);
            }

            PurchasesFetched?.Invoke(new List<ConfirmedPurchase>(PurchaseHistory));
        }

        /// <summary>Raises a late ProductsFetched event manually (simulates a race after Failed init).</summary>
        public void DeliverProductsFetched() => ProductsFetched?.Invoke(new List<StoreProduct>(Products));

        /// <summary>Raises ConnectionFailed manually (simulates a custom gateway's mid-session disconnect).</summary>
        public void DeliverConnectionFailed(string reason = "fake: dropped") => ConnectionFailed?.Invoke(reason);

        public void Purchase(string storeId)
        {
            PurchaseCalls.Add(storeId);
            if (AutoDeliverPendingOnPurchase)
                DeliverPending(storeId);
        }

        public PendingPurchase DeliverPending(string storeId, string transactionId = null)
        {
            var pending = new PendingPurchase(storeId, transactionId ?? $"tx_{NextTransactionNumber++}");
            PurchasePending?.Invoke(pending);
            return pending;
        }

        public void DeliverFailure(string storeId, string reason = "fake: user cancelled")
            => PurchaseFailed?.Invoke(new FailedPurchase(storeId, reason));

        public void Confirm(PendingPurchase pending)
        {
            ConfirmCalls.Add(pending);
            var confirmed = new ConfirmedPurchase(pending.StoreId, pending.TransactionId);
            PurchaseHistory.Add(confirmed);
            PurchaseConfirmed?.Invoke(confirmed);
        }

        public bool TryGetProduct(string storeId, out StoreProduct product)
        {
            foreach (var p in Products)
            {
                if (p.StoreId == storeId) { product = p; return true; }
            }

            product = default;
            return false;
        }
    }
}
