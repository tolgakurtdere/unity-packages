using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// Per-game ads configuration. Create via Assets > Create > TK > Ads Settings.
    /// Ad unit ids come from the AppLovin dashboard. Leave a format's ids empty to disable it.
    /// Consent note: MAX's Terms & Privacy Policy Flow is configured in AppLovin's Integration
    /// Manager (editor), not here — see README.
    /// </summary>
    [CreateAssetMenu(fileName = "AdsSettings", menuName = "TK/Ads Settings")]
    public class AdsSettings : ScriptableObject
    {
        [Header("Ad Units — Android")]
        public string androidBannerAdUnitId;
        public string androidInterstitialAdUnitId;
        public string androidRewardedAdUnitId;

        [Header("Ad Units — iOS")]
        public string iosBannerAdUnitId;
        public string iosInterstitialAdUnitId;
        public string iosRewardedAdUnitId;

        [Header("Banner")]
        public AdsBannerPosition bannerPosition = AdsBannerPosition.BottomCenter;
        public Color bannerBackgroundColor = Color.clear;

        [Header("Interstitial Pacing (defaults — IAdsPacingResolver can override at runtime)")]
        [Min(0)] public int interstitialMinIntervalSeconds = 60;
        [Min(0)] public int cooldownAfterRewardedSeconds = 60;

        public string BannerAdUnitId => SelectByPlatform(iosBannerAdUnitId, androidBannerAdUnitId);
        public string InterstitialAdUnitId => SelectByPlatform(iosInterstitialAdUnitId, androidInterstitialAdUnitId);
        public string RewardedAdUnitId => SelectByPlatform(iosRewardedAdUnitId, androidRewardedAdUnitId);

        private static string SelectByPlatform(string ios, string android)
        {
#if UNITY_IOS
            return ios;
#else
            return android; // Android + Editor default (reference had no #else — fixed)
#endif
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            WarnIfEmpty(androidBannerAdUnitId, iosBannerAdUnitId, "banner");
            WarnIfEmpty(androidInterstitialAdUnitId, iosInterstitialAdUnitId, "interstitial");
            WarnIfEmpty(androidRewardedAdUnitId, iosRewardedAdUnitId, "rewarded");
#endif
        }

#if UNITY_EDITOR
        private void WarnIfEmpty(string android, string ios, string format)
        {
            if (string.IsNullOrEmpty(android) && string.IsNullOrEmpty(ios))
                Debug.LogWarning($"[AdsSettings] '{name}': no {format} ad unit ids set — the {format} format will be disabled.", this);
        }
#endif
    }
}
