using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace TK.IAP
{
    /// <summary>
    /// String-keyed product catalog. One asset per game; ids are internal keys ("pack1",
    /// "remove_ads"), store ids are the store-console identifiers. Item types are game-defined
    /// strings handled via IapService.RegisterItemHandler; the reserved type "entitlement"
    /// grants Entitlements[Item.Value] automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "IapCatalog", menuName = "TK/IAP Catalog")]
    public class IapCatalog : ScriptableObject
    {
        [Serializable]
        public class Item
        {
            [Tooltip("Game-defined item type handled via RegisterItemHandler. Reserved: 'entitlement'.")]
            public string type;
            [Tooltip("Amount granted. Default value — an IIapAmountResolver can override at read time.")]
            public int amount = 1;
            [Tooltip("Optional string payload. For 'entitlement' items: the entitlement key.")]
            public string value;
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("Internal key used in code and UI bindings (e.g. 'pack1', 'remove_ads').")]
            public string id;
            [Tooltip("Store product identifier (same on both stores, per project convention).")]
            public string storeId;
            public ProductType productType = ProductType.Consumable;
            public StorePlatform platforms = StorePlatform.BothStores;
            public List<Item> items = new();

            /// <summary>
            /// Bool-typed escape hatch for assemblies that don't reference Unity.Purchasing
            /// (e.g. TK.IAP.UI, scoped to ["TK.IAP", "UnityEngine.UI", "Unity.TextMeshPro"]).
            /// </summary>
            public bool IsNonConsumable => productType == ProductType.NonConsumable;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, Entry> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(string id, out Entry entry)
        {
            _lookup ??= BuildLookup();
            return _lookup.TryGetValue(id, out entry) && entry != null;
        }

        /// <summary>Returns the entry for an id, or null (with an error log) if missing.</summary>
        public Entry Get(string id)
        {
            if (TryGet(id, out var entry)) return entry;
            Debug.LogError($"[IapCatalog] No entry for id '{id}' in catalog '{name}'.");
            return null;
        }

        /// <summary>Store fetch definitions for the given platform (true = Apple, false = Google).</summary>
        public List<StoreProductDefinition> BuildDefinitions(bool isApplePlatform)
        {
            var wanted = isApplePlatform ? StorePlatform.AppleAppStore : StorePlatform.GooglePlayStore;
            var definitions = new List<StoreProductDefinition>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.platforms == StorePlatform.BothStores || entry.platforms == wanted)
                    definitions.Add(new StoreProductDefinition(entry.storeId, entry.productType));
            }
            return definitions;
        }

        /// <summary>Replaces all entries. Intended for tests and programmatic catalog builds.</summary>
        public void SetEntries(List<Entry> newEntries)
        {
            entries = newEntries ?? new List<Entry>();
            _lookup = null;
        }

        private Dictionary<string, Entry> BuildLookup()
        {
            var lookup = new Dictionary<string, Entry>(entries.Count);
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.id)) continue;
                if (!lookup.TryAdd(entry.id, entry))
                    Debug.LogError($"[IapCatalog] Duplicate id '{entry.id}' in catalog '{name}'.");
            }
            return lookup;
        }

        private void OnValidate()
        {
            _lookup = null;
#if UNITY_EDITOR
            var seenIds = new HashSet<string>();
            var seenStoreIds = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.id))
                    Debug.LogWarning($"[IapCatalog] '{name}': entry with empty id.", this);
                else if (!seenIds.Add(entry.id))
                    Debug.LogWarning($"[IapCatalog] '{name}': duplicate id '{entry.id}'.", this);

                if (string.IsNullOrEmpty(entry.storeId))
                    Debug.LogWarning($"[IapCatalog] '{name}': entry '{entry.id}' has empty storeId.", this);
                else if (!seenStoreIds.Add(entry.storeId))
                    Debug.LogWarning($"[IapCatalog] '{name}': duplicate storeId '{entry.storeId}'.", this);

                if (entry.productType == ProductType.Subscription)
                    Debug.LogWarning($"[IapCatalog] '{name}': entry '{entry.id}' is a Subscription — not supported in com.tk.iap v0.1 (planned for a later version).", this);

                if (entry.productType == ProductType.Consumable && entry.items.Exists(item => item.type == IapService.EntitlementItemType))
                    Debug.LogWarning($"[IapCatalog] '{name}': consumable '{entry.id}' grants an entitlement — entitlements inside consumables do NOT survive reinstall (store history only restores NonConsumables). Prefer a NonConsumable product for permanent grants.", this);
            }
#endif
        }
    }
}
