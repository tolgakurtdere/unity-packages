using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// Official no-network <see cref="IAdsGateway"/> for editor runs, test builds, and demos —
    /// pass it as the service's gateway (e.g. behind <c>#if UNITY_EDITOR || TK_TEST_BUILD</c>) so
    /// the full ads flow runs without the MAX SDK or any scoped-registry setup. Two modes:
    /// <list type="bullet">
    /// <item><b>Auto</b> (default, <see cref="AutoResolveShows"/> = true): loads fill immediately
    /// and every Show resolves itself — Displayed, then RewardReceived (rewarded), then Hidden —
    /// so game flow (reward grants, pacing, mute push/pop) proceeds end-to-end unattended.</item>
    /// <item><b>Manual</b> (<see cref="AutoResolveShows"/> = false): Show raises only Displayed
    /// and waits for <see cref="CompleteRewarded"/> / <see cref="CancelRewarded"/> /
    /// <see cref="CloseInterstitial"/> / <see cref="FailInterstitial"/> — for demos and
    /// interactive testing (the AdsDemo sample drives this mode from ContextMenu entries).</item>
    /// </list>
    /// All events fire synchronously on the caller's thread — no frames pass between them.
    /// Not a recording probe: it has no call-capture surface; assert on the events themselves.
    /// </summary>
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

        // ── Behavior knobs (set before use; defaults are the unattended happy path) ──

        /// <summary>True (default): every Show resolves itself synchronously. False: Show raises only Displayed and waits for the manual resolution methods.</summary>
        public bool AutoResolveShows = true;

        /// <summary>Raise InitializeFailed instead of Initialized.</summary>
        public bool FailInit;

        /// <summary>When false, CreateBanner raises BannerLoadFailed instead of BannerLoaded.</summary>
        public bool BannerFill = true;

        /// <summary>When false, LoadInterstitial raises InterstitialLoadFailed and readiness stays false.</summary>
        public bool InterstitialFill = true;

        /// <summary>When false, LoadRewarded raises RewardedLoadFailed and readiness stays false.</summary>
        public bool RewardedFill = true;

        /// <summary>Result of <see cref="ShowConsentDialogAsync"/>.</summary>
        public bool ConsentDialogResult = true;

        public bool IsInterstitialReady { get; private set; }
        public bool IsRewardedReady { get; private set; }

        public Task InitializeAsync()
        {
            if (FailInit) InitializeFailed?.Invoke("fake: init failed");
            else Initialized?.Invoke();
            return Task.CompletedTask;
        }

        // ── Banner ──

        public void CreateBanner(string adUnitId, AdsBannerPosition position, Color backgroundColor)
        {
            if (BannerFill) BannerLoaded?.Invoke();
            else BannerLoadFailed?.Invoke("fake: no fill");
        }

        public void ShowBanner() { }
        public void HideBanner() { }
        public void DestroyBanner() { }

        /// <summary>Simulates a banner tap.</summary>
        public void ClickBanner() => BannerClicked?.Invoke();

        // ── Interstitial ──

        public void LoadInterstitial(string adUnitId)
        {
            if (!InterstitialFill)
            {
                InterstitialLoadFailed?.Invoke("fake: no fill");
                return;
            }

            IsInterstitialReady = true;
            InterstitialLoaded?.Invoke();
        }

        public void ShowInterstitial(string placement)
        {
            IsInterstitialReady = false;
            InterstitialDisplayed?.Invoke();
            if (AutoResolveShows) InterstitialHidden?.Invoke();
        }

        /// <summary>Manual mode: fires InterstitialHidden — call while an interstitial is showing.</summary>
        public void CloseInterstitial() => InterstitialHidden?.Invoke();

        /// <summary>Manual mode: fires InterstitialDisplayFailed — call while an interstitial is showing.</summary>
        public void FailInterstitial() => InterstitialDisplayFailed?.Invoke("fake: simulated display failure");

        // ── Rewarded ──

        public void LoadRewarded(string adUnitId)
        {
            if (!RewardedFill)
            {
                RewardedLoadFailed?.Invoke("fake: no fill");
                return;
            }

            IsRewardedReady = true;
            RewardedLoaded?.Invoke();
        }

        public void ShowRewarded(string placement)
        {
            IsRewardedReady = false;
            RewardedDisplayed?.Invoke();
            if (AutoResolveShows)
            {
                RewardReceived?.Invoke();
                RewardedHidden?.Invoke();
            }
        }

        /// <summary>Manual mode: fires RewardReceived then RewardedHidden — the player watched to the end.</summary>
        public void CompleteRewarded()
        {
            RewardReceived?.Invoke();
            RewardedHidden?.Invoke();
        }

        /// <summary>Manual mode: fires only RewardedHidden (no reward) — the player closed early.</summary>
        public void CancelRewarded() => RewardedHidden?.Invoke();

        /// <summary>Manual mode: fires RewardedDisplayFailed — call while a rewarded ad is showing.</summary>
        public void FailRewarded() => RewardedDisplayFailed?.Invoke("fake: simulated display failure");

        // ── Cross-cutting ──

        /// <summary>Emits RevenuePaid — for exercising revenue reporters/analytics bridges.</summary>
        public void RaiseRevenue(AdRevenueInfo info) => RevenuePaid?.Invoke(info);

        public Task<bool> ShowConsentDialogAsync() => Task.FromResult(ConsentDialogResult);
    }
}
