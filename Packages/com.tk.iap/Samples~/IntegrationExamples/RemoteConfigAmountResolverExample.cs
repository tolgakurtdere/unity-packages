using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TK.IAP;
using UnityEngine;

namespace TK.IAP.Samples.IntegrationExamples
{
    /// <summary>
    /// Reference <see cref="IIapAmountResolver"/> backed by a remote-config JSON string (e.g. from
    /// Firebase Remote Config, Unity Remote Config, or any other string-delivering backend). Uses
    /// Newtonsoft (<c>com.tk.iap</c> declares <c>com.unity.nuget.newtonsoft-json</c>).
    ///
    /// If your remote config comes from <c>com.tk.remoteconfig</c>, prefer its <c>GetObject&lt;T&gt;</c>
    /// (which does this Newtonsoft deserialization for you) over hand-rolling parsing here — this
    /// sample targets backends that only deliver a raw JSON string.
    ///
    /// Pattern: parse once per distinct JSON payload (cached until the source string changes),
    /// sanitize entries so a bad/zero/negative remote value can never break the game (falls back to
    /// the catalog's own default amount instead), and look up by "productId:itemType".
    /// </summary>
    public sealed class RemoteConfigAmountResolverExample : IIapAmountResolver
    {
        private class OverrideEntry
        {
            public string productId;
            public string itemType;
            public int amount;
        }

        private class OverrideList
        {
            public List<OverrideEntry> overrides = new();
        }

        private string _lastJson;
        private Dictionary<string, int> _lookup = new();

        /// <summary>
        /// Call this whenever the remote config value changes (e.g. from a fetch-and-activate
        /// callback). Safe to call every time the config is (re-)fetched — a no-op if the JSON is
        /// unchanged since the last call, since re-parsing on every Resolve() call would be wasteful.
        /// </summary>
        public void SetConfigJson(string json)
        {
            if (json == _lastJson) return; // unchanged since last parse — keep the existing lookup
            _lastJson = json;
            _lookup = Parse(json);
        }

        public int Resolve(string productId, string itemType, int defaultAmount)
        {
            var key = BuildKey(productId, itemType);
            return _lookup.TryGetValue(key, out var amount) ? amount : defaultAmount;
        }

        private static Dictionary<string, int> Parse(string json)
        {
            var result = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(json)) return result;

            OverrideList parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<OverrideList>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RemoteConfigAmountResolverExample] Failed to parse remote config JSON: {exception.Message}. Falling back to catalog defaults.");
                return result;
            }

            if (parsed?.overrides == null) return result;

            foreach (var entry in parsed.overrides)
            {
                if (string.IsNullOrEmpty(entry.productId) || string.IsNullOrEmpty(entry.itemType)) continue;

                // Sanitize: a non-positive remote amount is treated as absent (Resolve() then falls
                // back to defaultAmount) rather than granting zero/negative items.
                if (entry.amount <= 0)
                {
                    Debug.LogWarning($"[RemoteConfigAmountResolverExample] Ignoring non-positive amount for '{entry.productId}:{entry.itemType}' — using catalog default instead.");
                    continue;
                }

                result[BuildKey(entry.productId, entry.itemType)] = entry.amount;
            }

            return result;
        }

        private static string BuildKey(string productId, string itemType) => $"{productId}:{itemType}";
    }
}
