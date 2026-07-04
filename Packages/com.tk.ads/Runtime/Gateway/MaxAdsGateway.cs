using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// AppLovin MAX backed ads gateway. Placeholder — the real implementation lands in Task 5.
    /// Tests inject FakeAdsGateway, so nothing executes this.
    /// </summary>
    public sealed class MaxAdsGateway : IAdsGateway
    {
#pragma warning disable 0067 // placeholder events are never raised until Task 5 implements them
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
#pragma warning restore 0067

        public bool IsInterstitialReady => throw new NotImplementedException("Implemented in Task 5");
        public bool IsRewardedReady => throw new NotImplementedException("Implemented in Task 5");

        public Task InitializeAsync()
            => throw new NotImplementedException("Implemented in Task 5");

        public void CreateBanner(string adUnitId, AdsBannerPosition position, Color backgroundColor)
            => throw new NotImplementedException("Implemented in Task 5");

        public void ShowBanner()
            => throw new NotImplementedException("Implemented in Task 5");

        public void HideBanner()
            => throw new NotImplementedException("Implemented in Task 5");

        public void DestroyBanner()
            => throw new NotImplementedException("Implemented in Task 5");

        public void LoadInterstitial(string adUnitId)
            => throw new NotImplementedException("Implemented in Task 5");

        public void ShowInterstitial(string placement)
            => throw new NotImplementedException("Implemented in Task 5");

        public void LoadRewarded(string adUnitId)
            => throw new NotImplementedException("Implemented in Task 5");

        public void ShowRewarded(string placement)
            => throw new NotImplementedException("Implemented in Task 5");

        public Task<bool> ShowConsentDialogAsync()
            => throw new NotImplementedException("Implemented in Task 5");
    }
}
