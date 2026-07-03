using System.Collections.Generic;
using NUnit.Framework;

namespace TK.IAP.Tests
{
    [TestFixture]
    public sealed class FakeStoreGatewayTests
    {
        [Test]
        public void Connect_RaisesConnected()
        {
            var gateway = new FakeStoreGateway();
            var connected = false;
            gateway.Connected += () => connected = true;

            gateway.ConnectAsync().Wait();

            Assert.IsTrue(connected);
        }

        [Test]
        public void Purchase_AutoDeliversPending_WithUniqueTransactionIds()
        {
            var gateway = new FakeStoreGateway();
            var pendingPurchases = new List<PendingPurchase>();
            gateway.PurchasePending += pendingPurchases.Add;

            gateway.Purchase("coin_pack_1");
            gateway.Purchase("coin_pack_2");

            Assert.AreEqual(2, pendingPurchases.Count);
            Assert.AreEqual("tx_1", pendingPurchases[0].TransactionId);
            Assert.AreEqual("tx_2", pendingPurchases[1].TransactionId);
        }

        [Test]
        public void Confirm_MovesPendingToHistory_AndRaisesConfirmed()
        {
            var gateway = new FakeStoreGateway();
            ConfirmedPurchase raised = null;
            gateway.PurchaseConfirmed += confirmed => raised = confirmed;

            var pending = gateway.DeliverPending("coin_pack_1");
            gateway.Confirm(pending);

            Assert.AreEqual(1, gateway.PurchaseHistory.Count);
            Assert.AreEqual("coin_pack_1", gateway.PurchaseHistory[0].StoreId);
            Assert.AreEqual(pending.TransactionId, gateway.PurchaseHistory[0].TransactionId);
            Assert.IsNotNull(raised);
            Assert.AreEqual(pending.TransactionId, raised.TransactionId);
        }

        [Test]
        public void FetchProducts_FailsNTimesThenSucceeds()
        {
            var gateway = new FakeStoreGateway { FailProductFetchTimes = 1 };
            gateway.Products.Add(new StoreProduct("coin_pack_1", 0.99m, "USD", "$0.99", "Coin Pack"));

            string failReason = null;
            IReadOnlyList<StoreProduct> fetched = null;
            gateway.ProductsFetchFailed += reason => failReason = reason;
            gateway.ProductsFetched += products => fetched = products;

            // Empty list is enough: constructing a StoreProductDefinition needs a
            // UnityEngine.Purchasing.ProductType, which this test asmdef does not
            // reference (only TK.IAP, TK.Core.Save, TestRunner assemblies per spec).
            // The failure path never inspects definitions, and the success path's
            // filter against an empty list yields an empty (non-null) result, which
            // is sufficient to prove ProductsFetched fired instead of the fail event.
            var definitions = new List<StoreProductDefinition>();

            gateway.FetchProducts(definitions);

            Assert.IsNotNull(failReason);
            Assert.IsNull(fetched);

            failReason = null;
            gateway.FetchProducts(definitions);

            Assert.IsNull(failReason);
            Assert.IsNotNull(fetched);
            Assert.AreEqual(0, fetched.Count);
            Assert.AreEqual(2, gateway.FetchProductsCalls);
        }
    }
}
