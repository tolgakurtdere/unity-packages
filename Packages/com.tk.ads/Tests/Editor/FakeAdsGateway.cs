using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TK.Ads;

namespace TK.Ads.Tests
{
    public sealed class FakeAdsGateway : IAdsGateway
    {
        public event Action Initialized;
        public event Action<string> InitializeFailed;
        public event Action BannerLoaded;
        public event Action<string> BannerLoadFailed;
        public event Action BannerClicked;
        public event Action InterstitialLoaded;
        public event Action<string> InterstitialLoadFailed;
        public event Action InterstitialDisplayed;
        public event Action<string> InterstitialDisplayFailed;
        public event Action InterstitialHidden;
        public event Action RewardedLoaded;
        public event Action<string> RewardedLoadFailed;
        public event Action RewardedDisplayed;
        public event Action<string> RewardedDisplayFailed;
        public event Action RewardedHidden;
        public event Action RewardReceived;
        public event Action<AdRevenueInfo> RevenuePaid;

        // ── Scripting knobs ──
        public bool FailInit;
        public bool ThrowOnInit;
        public bool ConsentDialogResult = true;

        // ── Recorded state ──
        public string CreatedBannerAdUnitId;
        public AdsBannerPosition CreatedBannerPosition;
        public Color CreatedBannerColor;
        public int ShowBannerCalls;
        public int HideBannerCalls;
        public int DestroyBannerCalls;
        public readonly List<string> LoadInterstitialCalls = new();
        public readonly List<string> LoadRewardedCalls = new();
        public readonly List<string> ShowInterstitialPlacements = new();
        public readonly List<string> ShowRewardedPlacements = new();
        public int ConsentDialogCalls;

        // ── Readiness ──
        public bool InterstitialReady;
        public bool RewardedReady;
        public bool IsInterstitialReady => InterstitialReady;
        public bool IsRewardedReady => RewardedReady;

        public Task InitializeAsync()
        {
            if (ThrowOnInit) throw new InvalidOperationException("fake: init exploded");
            if (FailInit) InitializeFailed?.Invoke("fake: init failed");
            else Initialized?.Invoke();
            return Task.CompletedTask;
        }

        public void CreateBanner(string adUnitId, AdsBannerPosition position, Color backgroundColor)
        {
            CreatedBannerAdUnitId = adUnitId;
            CreatedBannerPosition = position;
            CreatedBannerColor = backgroundColor;
        }

        public void ShowBanner() => ShowBannerCalls++;
        public void HideBanner() => HideBannerCalls++;
        public void DestroyBanner() => DestroyBannerCalls++;

        public void LoadInterstitial(string adUnitId) => LoadInterstitialCalls.Add(adUnitId);
        public void ShowInterstitial(string placement) => ShowInterstitialPlacements.Add(placement);

        public void LoadRewarded(string adUnitId) => LoadRewardedCalls.Add(adUnitId);
        public void ShowRewarded(string placement) => ShowRewardedPlacements.Add(placement);

        public Task<bool> ShowConsentDialogAsync()
        {
            ConsentDialogCalls++;
            return Task.FromResult(ConsentDialogResult);
        }

        // ── Manual event delivery (tests drive the gateway's timeline explicitly) ──
        public void DeliverInitialized() => Initialized?.Invoke();
        public void DeliverInitializeFailed(string message) => InitializeFailed?.Invoke(message);
        public void DeliverBannerLoaded() => BannerLoaded?.Invoke();
        public void DeliverBannerLoadFailed(string message) => BannerLoadFailed?.Invoke(message);
        public void DeliverBannerClicked() => BannerClicked?.Invoke();
        public void DeliverInterstitialLoaded() => InterstitialLoaded?.Invoke();
        public void DeliverInterstitialLoadFailed(string message) => InterstitialLoadFailed?.Invoke(message);
        public void DeliverInterstitialDisplayed() => InterstitialDisplayed?.Invoke();
        public void DeliverInterstitialDisplayFailed(string message) => InterstitialDisplayFailed?.Invoke(message);
        public void DeliverInterstitialHidden() => InterstitialHidden?.Invoke();
        public void DeliverRewardedLoaded() => RewardedLoaded?.Invoke();
        public void DeliverRewardedLoadFailed(string message) => RewardedLoadFailed?.Invoke(message);
        public void DeliverRewardedDisplayed() => RewardedDisplayed?.Invoke();
        public void DeliverRewardedDisplayFailed(string message) => RewardedDisplayFailed?.Invoke(message);
        public void DeliverRewardedHidden() => RewardedHidden?.Invoke();
        public void DeliverRewardReceived() => RewardReceived?.Invoke();
        public void DeliverRevenue(AdRevenueInfo info) => RevenuePaid?.Invoke(info);
    }
}
