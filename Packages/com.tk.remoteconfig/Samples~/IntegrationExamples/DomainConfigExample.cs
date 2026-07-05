using System.Collections.Generic;
using TK.RemoteConfig;
using UnityEngine;

namespace TK.RemoteConfig.Samples.IntegrationExamples
{
    /// <summary>
    /// Demonstrates the <b>recommended</b> config-modeling pattern: one remote-config key per
    /// <i>domain</i>, holding a single JSON object deserialized to a typed class via
    /// <see cref="RemoteConfigService.GetObject{T}"/> (Newtonsoft).
    ///
    /// Why one JSON per domain instead of dozens of scalar keys (or CSV lists)?
    /// <list type="bullet">
    /// <item>One console entry per feature area (ads / iap / economy) instead of a wall of keys.</item>
    /// <item>Add a field without touching every call site — just extend the class; old payloads
    /// missing the field fall back to the C# default.</item>
    /// <item>Newtonsoft handles what <c>JsonUtility</c> can't: <c>Dictionary&lt;,&gt;</c>, nested
    /// objects, top-level arrays, nullable/optional fields, enum-as-string.</item>
    /// </list>
    ///
    /// Reach for the scalar factories (<c>rc.Int</c>/<c>rc.Bool</c>/...) for individual flags and the
    /// per-platform key overloads; reach for <c>GetObject&lt;T&gt;</c> for a grouped feature config.
    /// </summary>
    public sealed class DomainConfigExample : MonoBehaviour
    {
        // ── Plain config classes. Newtonsoft needs NO [Serializable] and reads public fields (and
        //    properties). Missing JSON fields keep the C# default; a bad payload → the passed default. ──

        private class EconomyConfig
        {
            public int StartingCoins = 100;

            // A dictionary — impossible with JsonUtility, trivial with Newtonsoft.
            public Dictionary<string, int> ItemPrices = new();
        }

        private class AdsConfig
        {
            public int InterstitialInterval = 30;
            public int BannerStartLevel = 3;
        }

        private void Start()
        {
            var rc = RemoteConfigService.Instance;
            if (rc == null) return; // construct + InitializeAsync a service elsewhere first

            // One key per domain. Console value for "economy_config" might be:
            //   { "StartingCoins": 250, "ItemPrices": { "sword": 500, "shield": 300 } }
            var economy = rc.GetObject("economy_config", new EconomyConfig());

            // Console value for "ads_config":
            //   { "InterstitialInterval": 45, "BannerStartLevel": 5 }
            var ads = rc.GetObject("ads_config", new AdsConfig());

            Debug.Log($"[DomainConfig] startingCoins={economy.StartingCoins}, " +
                      $"itemPrices={economy.ItemPrices.Count} entries, " +
                      $"interstitialInterval={ads.InterstitialInterval}, bannerStartLevel={ads.BannerStartLevel}");
        }
    }
}
