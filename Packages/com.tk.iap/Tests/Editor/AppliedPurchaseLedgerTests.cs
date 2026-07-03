using NUnit.Framework;

namespace TK.IAP.Tests
{
    [TestFixture]
    public sealed class AppliedPurchaseLedgerTests
    {
        [Test]
        public void Transaction_MarkAndCheck_PersistsAcrossInstances()
        {
            var save = new FakeSaveSystem();
            var ledger = new AppliedPurchaseLedger(save);

            Assert.IsFalse(ledger.IsTransactionApplied("tx_1"));

            ledger.MarkTransactionApplied("tx_1");

            Assert.IsTrue(ledger.IsTransactionApplied("tx_1"));

            var reloaded = new AppliedPurchaseLedger(save);
            Assert.IsTrue(reloaded.IsTransactionApplied("tx_1"));
        }

        [Test]
        public void Transactions_TrimmedBeyond100_OldestForgotten()
        {
            var save = new FakeSaveSystem();
            var ledger = new AppliedPurchaseLedger(save);

            for (var i = 0; i <= 100; i++)
            {
                ledger.MarkTransactionApplied($"tx_{i}");
            }

            Assert.IsFalse(ledger.IsTransactionApplied("tx_0"));
            Assert.IsTrue(ledger.IsTransactionApplied("tx_100"));
        }

        [Test]
        public void Product_MarkAndCheck_Persists()
        {
            var save = new FakeSaveSystem();
            var ledger = new AppliedPurchaseLedger(save);

            Assert.IsFalse(ledger.IsProductApplied("no_ads"));

            ledger.MarkProductApplied("no_ads");

            Assert.IsTrue(ledger.IsProductApplied("no_ads"));

            var reloaded = new AppliedPurchaseLedger(save);
            Assert.IsTrue(reloaded.IsProductApplied("no_ads"));
        }

        [Test]
        public void EmptyOrNullIds_AreNeverApplied_AndSafeToMark()
        {
            var save = new FakeSaveSystem();
            var ledger = new AppliedPurchaseLedger(save);

            Assert.IsFalse(ledger.IsTransactionApplied(null));
            Assert.IsFalse(ledger.IsTransactionApplied(string.Empty));
            Assert.IsFalse(ledger.IsProductApplied(null));
            Assert.IsFalse(ledger.IsProductApplied(string.Empty));

            Assert.DoesNotThrow(() => ledger.MarkTransactionApplied(null));
            Assert.DoesNotThrow(() => ledger.MarkTransactionApplied(string.Empty));
            Assert.DoesNotThrow(() => ledger.MarkProductApplied(null));
            Assert.DoesNotThrow(() => ledger.MarkProductApplied(string.Empty));

            Assert.IsFalse(ledger.IsTransactionApplied(null));
            Assert.IsFalse(ledger.IsTransactionApplied(string.Empty));
            Assert.IsFalse(ledger.IsProductApplied(null));
            Assert.IsFalse(ledger.IsProductApplied(string.Empty));
        }
    }
}
