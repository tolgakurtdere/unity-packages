using System;
using System.Collections.Generic;
using TK.Core.Save;

namespace TK.IAP
{
    /// <summary>
    /// Tracks which purchases were already applied to the game, so re-delivered orders
    /// (crash recovery, session restores) never double-grant. Transactions are kept in a
    /// bounded FIFO (consumables); products are kept forever (non-consumables).
    /// Because the package tracks application, item handlers do NOT need to be idempotent.
    /// </summary>
    public sealed class AppliedPurchaseLedger
    {
        private const string TxSaveKey = "tk_iap_applied_tx";
        private const string ProductSaveKey = "tk_iap_applied_products";
        private const int MaxTrackedTransactions = 100;

        private readonly ISaveSystem _save;
        private readonly List<string> _transactions;   // FIFO order preserved for trimming
        private readonly HashSet<string> _transactionSet;
        private readonly HashSet<string> _products;

        public AppliedPurchaseLedger(ISaveSystem save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _transactions = _save.Load(TxSaveKey, new List<string>());
            _transactionSet = new HashSet<string>(_transactions);
            _products = new HashSet<string>(_save.Load(ProductSaveKey, new List<string>()));
        }

        public bool IsTransactionApplied(string transactionId)
            => !string.IsNullOrEmpty(transactionId) && _transactionSet.Contains(transactionId);

        public void MarkTransactionApplied(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId) || !_transactionSet.Add(transactionId)) return;

            _transactions.Add(transactionId);
            while (_transactions.Count > MaxTrackedTransactions)
            {
                _transactionSet.Remove(_transactions[0]);
                _transactions.RemoveAt(0);
            }

            _save.Save(TxSaveKey, _transactions);
        }

        public bool IsProductApplied(string productId)
            => !string.IsNullOrEmpty(productId) && _products.Contains(productId);

        public void MarkProductApplied(string productId)
        {
            if (string.IsNullOrEmpty(productId) || !_products.Add(productId)) return;
            _save.Save(ProductSaveKey, new List<string>(_products));
        }
    }
}
