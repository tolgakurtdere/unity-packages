using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TK.Core.UI
{
    /// <summary>
    /// String-keyed catalog of UI prefab addressable references.
    /// Games create one asset, fill it with their layouts/popups, and assign it to the UIManager.
    /// </summary>
    [CreateAssetMenu(fileName = "UICatalog", menuName = "TK/UI Catalog")]
    public class UICatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public AssetReferenceGameObject asset;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, AssetReferenceGameObject> _lookup;

        public bool TryGet(string key, out AssetReferenceGameObject asset)
        {
            _lookup ??= BuildLookup();
            return _lookup.TryGetValue(key, out asset) && asset != null;
        }

        /// <summary>Returns the reference for a key, or null (with an error log) if missing.</summary>
        public AssetReferenceGameObject Get(string key)
        {
            if (TryGet(key, out var asset)) return asset;
            Debug.LogError($"[UICatalog] No entry for key '{key}' in catalog '{name}'.");
            return null;
        }

        private Dictionary<string, AssetReferenceGameObject> BuildLookup()
        {
            var lookup = new Dictionary<string, AssetReferenceGameObject>(entries.Count);
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.key)) continue;
                if (!lookup.TryAdd(entry.key, entry.asset))
                    Debug.LogError($"[UICatalog] Duplicate key '{entry.key}' in catalog '{name}'.");
            }
            return lookup;
        }

        private void OnValidate() => _lookup = null;
    }
}
