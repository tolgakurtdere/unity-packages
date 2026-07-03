using System;
using System.Collections.Generic;
using TK.Core.Save;
using UnityEngine;

namespace TK.IAP
{
    /// <summary>
    /// Persistent boolean grants ("remove_ads", "vip", ...). v1 is grant-only (no revoke) —
    /// subscriptions will add revocation in a later version. Other packages (e.g. Ads) react
    /// to entitlements via Subscribe; this package never calls into them.
    /// </summary>
    public sealed class Entitlements
    {
        private const string SaveKey = "tk_iap_entitlements";

        private readonly ISaveSystem _save;
        private readonly HashSet<string> _granted;
        private readonly Dictionary<string, Action> _subscribers = new();

        /// <summary>Raised once per newly granted key. Not raised for already-granted keys.</summary>
        public event Action<string> Changed;

        public Entitlements(ISaveSystem save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _granted = new HashSet<string>(_save.Load(SaveKey, new List<string>()));
        }

        public bool Has(string key) => !string.IsNullOrEmpty(key) && _granted.Contains(key);

        /// <summary>Grants a key (idempotent). Public so games can grant via promos/debug menus.</summary>
        public void Grant(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[Entitlements] Grant called with empty key.");
                return;
            }

            if (!_granted.Add(key)) return;

            _save.Save(SaveKey, new List<string>(_granted));
            Changed?.Invoke(key);

            if (_subscribers.TryGetValue(key, out var callbacks))
            {
                _subscribers.Remove(key); // latch: a granted key can never fire again, so drop it
                callbacks?.Invoke();
            }
        }

        /// <summary>
        /// Latch subscription: fires immediately if the key is already granted, otherwise
        /// fires once when it becomes granted. (Pattern from the reference MyIAPManager.)
        /// </summary>
        public void Subscribe(string key, Action onGranted)
        {
            if (string.IsNullOrEmpty(key) || onGranted == null) return;

            if (Has(key))
            {
                onGranted();
                return;
            }

            if (_subscribers.TryGetValue(key, out var existing)) _subscribers[key] = existing + onGranted;
            else _subscribers[key] = onGranted;
        }

        public void Unsubscribe(string key, Action onGranted)
        {
            if (string.IsNullOrEmpty(key) || onGranted == null) return;
            if (!_subscribers.TryGetValue(key, out var existing)) return;

            existing -= onGranted;
            if (existing == null) _subscribers.Remove(key);
            else _subscribers[key] = existing;
        }
    }
}
