using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// AppLovin MAX backed ads gateway. All MaxSdk callbacks arrive on whatever thread the
    /// native SDK chooses; every handler here re-filters by stored ad unit id and marshals to
    /// the Unity main thread via <see cref="MainThreadDispatcher"/> before raising the seam event.
    /// The Unity Editor cannot display real MAX ads (no fill, no native views) — use
    /// FakeAdsGateway-backed demos for editor playtesting, and AppLovin's Mediation Debugger
    /// (<c>MaxSdk.ShowMediationDebugger()</c>) on-device. The consent (Terms &amp; Privacy Policy)
    /// flow shown at first SDK init is configured in the AppLovin Integration Manager, not here;
    /// <see cref="ShowConsentDialogAsync"/> only re-prompts an existing user via the CMP service.
    /// </summary>
    public sealed class MaxAdsGateway : IAdsGateway
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

        private readonly MainThreadDispatcher _dispatcher = new();

        private string _bannerAdUnitId;
        private string _interstitialAdUnitId;
        private string _rewardedAdUnitId;
        private bool _bannerCreated;
        private string _lastInterstitialPlacement;
        private string _lastRewardedPlacement;

        public bool IsInterstitialReady =>
            !string.IsNullOrEmpty(_interstitialAdUnitId) && MaxSdk.IsInterstitialReady(_interstitialAdUnitId);

        public bool IsRewardedReady =>
            !string.IsNullOrEmpty(_rewardedAdUnitId) && MaxSdk.IsRewardedAdReady(_rewardedAdUnitId);

        public Task InitializeAsync()
        {
            _dispatcher.CaptureMainThread();
            var tcs = new TaskCompletionSource<bool>();

            MaxSdkCallbacks.OnSdkInitializedEvent += _ => _dispatcher.Post(() =>
            {
                Initialized?.Invoke();
                tcs.TrySetResult(true);
            });

            SubscribeBannerEvents();
            SubscribeInterstitialEvents();
            SubscribeRewardedEvents();

            try
            {
                MaxSdk.InitializeSdk();
            }
            catch (Exception exception)
            {
                // MAX 8.6.2 has no init-failure callback — InitializeSdk() is fire-and-forget and
                // reports success asynchronously via OnSdkInitializedEvent only. The only failure
                // signal available here is a synchronous throw out of the call itself.
                var message = exception.Message;
                _dispatcher.Post(() => InitializeFailed?.Invoke(message));
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        // ── Banner ──

        public void CreateBanner(string adUnitId, AdsBannerPosition position, Color backgroundColor)
        {
            _bannerAdUnitId = adUnitId;
            _bannerCreated = true;
            var configuration = new MaxSdkBase.AdViewConfiguration(ToMaxPosition(position));
            MaxSdk.CreateBanner(adUnitId, configuration);
            MaxSdk.SetBannerBackgroundColor(adUnitId, backgroundColor);
        }

        public void ShowBanner()
        {
            if (!RequireBannerCreated(nameof(ShowBanner))) return;
            MaxSdk.ShowBanner(_bannerAdUnitId);
        }

        public void HideBanner()
        {
            if (!RequireBannerCreated(nameof(HideBanner))) return;
            MaxSdk.HideBanner(_bannerAdUnitId);
        }

        public void DestroyBanner()
        {
            if (!RequireBannerCreated(nameof(DestroyBanner))) return;
            MaxSdk.DestroyBanner(_bannerAdUnitId);
            _bannerCreated = false;
        }

        private bool RequireBannerCreated(string caller)
        {
            if (_bannerCreated) return true;
            Debug.LogError($"[MaxAdsGateway] {caller}() called before CreateBanner()");
            return false;
        }

        private void SubscribeBannerEvents()
        {
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += (adUnitId, _) =>
            {
                if (adUnitId != _bannerAdUnitId) return;
                _dispatcher.Post(() => BannerLoaded?.Invoke());
            };

            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
            {
                if (adUnitId != _bannerAdUnitId) return;
                var message = errorInfo?.Message ?? "unknown error";
                _dispatcher.Post(() => BannerLoadFailed?.Invoke(message));
            };

            MaxSdkCallbacks.Banner.OnAdClickedEvent += (adUnitId, _) =>
            {
                if (adUnitId != _bannerAdUnitId) return;
                _dispatcher.Post(() => BannerClicked?.Invoke());
            };

            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                if (adUnitId != _bannerAdUnitId) return;
                var info = ToRevenueInfo("banner", adInfo, null);
                _dispatcher.Post(() => RevenuePaid?.Invoke(info));
            };
        }

        // ── Interstitial ──

        public void LoadInterstitial(string adUnitId)
        {
            _interstitialAdUnitId = adUnitId;
            MaxSdk.LoadInterstitial(adUnitId);
        }

        public void ShowInterstitial(string placement)
        {
            _lastInterstitialPlacement = placement;
            MaxSdk.ShowInterstitial(_interstitialAdUnitId, placement);
        }

        private void SubscribeInterstitialEvents()
        {
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (adUnitId, _) =>
            {
                if (adUnitId != _interstitialAdUnitId) return;
                _dispatcher.Post(() => InterstitialLoaded?.Invoke());
            };

            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
            {
                if (adUnitId != _interstitialAdUnitId) return;
                var message = errorInfo?.Message ?? "unknown error";
                _dispatcher.Post(() => InterstitialLoadFailed?.Invoke(message));
            };

            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += (adUnitId, _) =>
            {
                if (adUnitId != _interstitialAdUnitId) return;
                _dispatcher.Post(() => InterstitialDisplayed?.Invoke());
            };

            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (adUnitId, errorInfo, _) =>
            {
                if (adUnitId != _interstitialAdUnitId) return;
                var message = errorInfo?.Message ?? "unknown error";
                _dispatcher.Post(() => InterstitialDisplayFailed?.Invoke(message));
            };

            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (adUnitId, _) =>
            {
                if (adUnitId != _interstitialAdUnitId) return;
                _dispatcher.Post(() => InterstitialHidden?.Invoke());
            };

            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                if (adUnitId != _interstitialAdUnitId) return;
                var info = ToRevenueInfo("interstitial", adInfo, _lastInterstitialPlacement);
                _dispatcher.Post(() => RevenuePaid?.Invoke(info));
            };
        }

        // ── Rewarded ──

        public void LoadRewarded(string adUnitId)
        {
            _rewardedAdUnitId = adUnitId;
            MaxSdk.LoadRewardedAd(adUnitId);
        }

        public void ShowRewarded(string placement)
        {
            _lastRewardedPlacement = placement;
            MaxSdk.ShowRewardedAd(_rewardedAdUnitId, placement);
        }

        private void SubscribeRewardedEvents()
        {
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (adUnitId, _) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                _dispatcher.Post(() => RewardedLoaded?.Invoke());
            };

            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                var message = errorInfo?.Message ?? "unknown error";
                _dispatcher.Post(() => RewardedLoadFailed?.Invoke(message));
            };

            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += (adUnitId, _) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                _dispatcher.Post(() => RewardedDisplayed?.Invoke());
            };

            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (adUnitId, errorInfo, _) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                var message = errorInfo?.Message ?? "unknown error";
                _dispatcher.Post(() => RewardedDisplayFailed?.Invoke(message));
            };

            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (adUnitId, _) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                _dispatcher.Post(() => RewardedHidden?.Invoke());
            };

            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (adUnitId, _, __) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                _dispatcher.Post(() => RewardReceived?.Invoke());
            };

            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                if (adUnitId != _rewardedAdUnitId) return;
                var info = ToRevenueInfo("rewarded", adInfo, _lastRewardedPlacement);
                _dispatcher.Post(() => RevenuePaid?.Invoke(info));
            };
        }

        // ── Cross-cutting ──

        public Task<bool> ShowConsentDialogAsync()
        {
            if (!MaxSdk.CmpService.HasSupportedCmp) return Task.FromResult(false);

            var tcs = new TaskCompletionSource<bool>();
            MaxSdk.CmpService.ShowCmpForExistingUser(error => _dispatcher.Post(() => tcs.TrySetResult(error == null)));
            return tcs.Task;
        }

        private static AdRevenueInfo ToRevenueInfo(string format, MaxSdkBase.AdInfo adInfo, string placement)
        {
            if (adInfo == null) return new AdRevenueInfo(format, "", "", 0, "USD", placement);
            return new AdRevenueInfo(format, adInfo.NetworkName ?? "", adInfo.AdUnitIdentifier ?? "", adInfo.Revenue, "USD", placement);
        }

        private static MaxSdkBase.AdViewPosition ToMaxPosition(AdsBannerPosition position) => position switch
        {
            AdsBannerPosition.TopLeft => MaxSdkBase.AdViewPosition.TopLeft,
            AdsBannerPosition.TopCenter => MaxSdkBase.AdViewPosition.TopCenter,
            AdsBannerPosition.TopRight => MaxSdkBase.AdViewPosition.TopRight,
            AdsBannerPosition.Centered => MaxSdkBase.AdViewPosition.Centered,
            AdsBannerPosition.CenterLeft => MaxSdkBase.AdViewPosition.CenterLeft,
            AdsBannerPosition.CenterRight => MaxSdkBase.AdViewPosition.CenterRight,
            AdsBannerPosition.BottomLeft => MaxSdkBase.AdViewPosition.BottomLeft,
            AdsBannerPosition.BottomCenter => MaxSdkBase.AdViewPosition.BottomCenter,
            AdsBannerPosition.BottomRight => MaxSdkBase.AdViewPosition.BottomRight,
            _ => MaxSdkBase.AdViewPosition.BottomCenter
        };
    }
}
