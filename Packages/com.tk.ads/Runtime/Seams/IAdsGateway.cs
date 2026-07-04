using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// Ad-network seam. The package ships MaxAdsGateway (AppLovin MAX); tests inject fakes.
    /// CONTRACT: implementations must raise ALL events on the Unity main thread, and only
    /// after InitializeAsync was called. Load methods are one-shot requests — the service
    /// owns retry policy. Show methods act on the last successfully loaded ad.
    /// </summary>
    public interface IAdsGateway
    {
        Task InitializeAsync();
        event Action Initialized;
        event Action<string> InitializeFailed;

        // ── Banner (auto-refreshing view; Loaded may fire repeatedly) ──
        void CreateBanner(string adUnitId, AdsBannerPosition position, Color backgroundColor);
        void ShowBanner();
        void HideBanner();
        void DestroyBanner();
        event Action BannerLoaded;
        event Action<string> BannerLoadFailed;
        event Action BannerClicked;

        // ── Interstitial ──
        void LoadInterstitial(string adUnitId);
        bool IsInterstitialReady { get; }
        void ShowInterstitial(string placement);
        event Action InterstitialLoaded;
        event Action<string> InterstitialLoadFailed;
        event Action InterstitialDisplayed;
        event Action<string> InterstitialDisplayFailed;
        event Action InterstitialHidden;

        // ── Rewarded ──
        void LoadRewarded(string adUnitId);
        bool IsRewardedReady { get; }
        void ShowRewarded(string placement);
        event Action RewardedLoaded;
        event Action<string> RewardedLoadFailed;
        event Action RewardedDisplayed;
        event Action<string> RewardedDisplayFailed;
        event Action RewardedHidden;
        /// <summary>User earned the reward (fires DURING display, before Hidden).</summary>
        event Action RewardReceived;

        // ── Cross-cutting ──
        event Action<AdRevenueInfo> RevenuePaid;
        /// <summary>CMP "privacy options" re-prompt. False when no supported CMP or flow failed.</summary>
        Task<bool> ShowConsentDialogAsync();
    }
}
