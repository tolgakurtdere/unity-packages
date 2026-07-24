using TK.Ads;
using UnityEngine;

namespace TK.Ads.Samples.AdsDemo
{
    /// <summary>
    /// Editor-runnable demo of the full <see cref="AdsService"/> flow. Runs against the package's
    /// official no-network <see cref="FakeAdsGateway"/> (in manual mode) instead of real AppLovin
    /// MAX, because the Unity Editor cannot display real MAX ads (no fill, no native views). The
    /// same gateway, in its default auto mode, is what a game wires behind
    /// <c>#if UNITY_EDITOR || TK_TEST_BUILD</c> to run its ads flow without MAX. For
    /// device-accurate testing of real MAX ads, use AppLovin's Mediation Debugger
    /// (<c>MaxSdk.ShowMediationDebugger()</c>) on a real build instead — see this folder's README.
    ///
    /// Attach to any GameObject and press Play. Loads fill immediately in the fake gateway, so the
    /// interstitial/rewarded ads are ready right after <c>Initialized</c> fires. Use the
    /// <c>[ContextMenu]</c> entries (right-click the component header in the Inspector, or the
    /// component's "⋮" menu) to show ads and to simulate their outcomes.
    /// </summary>
    public class AdsDemo : MonoBehaviour
    {
        [Header("Ad Unit Ids (fake — FakeAdsGateway does not call any real network)")]
        [SerializeField] private string bannerAdUnitId = "demo-banner";
        [SerializeField] private string interstitialAdUnitId = "demo-interstitial";
        [SerializeField] private string rewardedAdUnitId = "demo-rewarded";

        private AdsService _ads;
        private FakeAdsGateway _gateway;

        private async void Start()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            settings.androidBannerAdUnitId = bannerAdUnitId;
            settings.androidInterstitialAdUnitId = interstitialAdUnitId;
            settings.androidRewardedAdUnitId = rewardedAdUnitId;

            // Manual mode: shows wait for the [ContextMenu] "Simulate: ..." entries below, so you
            // can act out complete/cancel/fail outcomes yourself. A game's editor/test wiring
            // would leave AutoResolveShows at its default (true) for unattended full cycles.
            _gateway = new FakeAdsGateway { AutoResolveShows = false };

            var options = new AdsOptions
            {
                Gateway = _gateway,
                RevenueReporter = new LoggingRevenueReporter(),

                // RemoveAds wiring would normally look like:
                //   ShouldShowInterstitial = () => !iap.Entitlements.Has("remove_ads"),
                // Left null here (= always allowed) since this sample has no com.tk.iap dependency.
                ShouldLoadBanner = () => true,
                ShouldShowBanner = () => true,
                ShouldLoadInterstitial = () => true,
                ShouldShowInterstitial = () => true,

                AudioMuteSetter = muted => Debug.Log($"[AdsDemo] AudioMuteSetter({muted})")
            };

            _ads = new AdsService(settings, options);
            _ads.Initialized += () => Debug.Log("[AdsDemo] Initialized.");
            _ads.InitFailed += () => Debug.Log("[AdsDemo] InitFailed.");
            _ads.BannerClicked += () => Debug.Log("[AdsDemo] BannerClicked.");
            _ads.InterstitialClosed += () => Debug.Log("[AdsDemo] InterstitialClosed.");
            _ads.RewardedReadyChanged += () => Debug.Log($"[AdsDemo] RewardedReadyChanged: IsRewardedReady={_ads.IsRewardedReady}.");

            Debug.Log("[AdsDemo] Calling InitializeAsync...");
            await _ads.InitializeAsync();
            Debug.Log($"[AdsDemo] Init finished with state: {_ads.State}");
        }

        // ── Banner ──

        [ContextMenu("Show Banner")]
        private void ShowBanner()
        {
            if (!RequireInitialized()) return;
            _ads.ShowBanner();
            Debug.Log($"[AdsDemo] ShowBanner() called. IsBannerVisible={_ads.IsBannerVisible}");
        }

        [ContextMenu("Hide Banner")]
        private void HideBanner()
        {
            if (!RequireInitialized()) return;
            _ads.HideBanner();
            Debug.Log("[AdsDemo] HideBanner() called.");
        }

        [ContextMenu("Destroy Banner")]
        private void DestroyBanner()
        {
            if (!RequireInitialized()) return;
            _ads.DestroyBanner();
            Debug.Log("[AdsDemo] DestroyBanner() called.");
        }

        // ── Interstitial ──

        [ContextMenu("Show Interstitial")]
        private async void ShowInterstitial()
        {
            if (!RequireInitialized()) return;
            Debug.Log($"[AdsDemo] ShowInterstitialAsync... IsInterstitialReady={_ads.IsInterstitialReady}");
            var shown = await _ads.ShowInterstitialAsync("demo_button");
            Debug.Log($"[AdsDemo] ShowInterstitialAsync result: {shown}");
        }

        /// <summary>Simulates the player closing the currently displayed interstitial.</summary>
        [ContextMenu("Simulate: Close Interstitial")]
        private void CloseInterstitial() => _gateway?.CloseInterstitial();

        /// <summary>Simulates the interstitial failing to display after being requested.</summary>
        [ContextMenu("Simulate: Fail Interstitial")]
        private void FailInterstitial() => _gateway?.FailInterstitial();

        // ── Rewarded ──

        [ContextMenu("Show Rewarded")]
        private async void ShowRewarded()
        {
            if (!RequireInitialized()) return;
            Debug.Log($"[AdsDemo] ShowRewardedAsync... IsRewardedReady={_ads.IsRewardedReady}");
            var result = await _ads.ShowRewardedAsync("demo_button");
            Debug.Log($"[AdsDemo] ShowRewardedAsync result: {result}");
        }

        /// <summary>Simulates the player watching the rewarded ad to completion.</summary>
        [ContextMenu("Simulate: Complete Rewarded")]
        private void CompleteRewarded() => _gateway?.CompleteRewarded();

        /// <summary>Simulates the player closing the rewarded ad early (no reward).</summary>
        [ContextMenu("Simulate: Cancel Rewarded")]
        private void CancelRewarded() => _gateway?.CancelRewarded();

        // ── Consent ──

        [ContextMenu("Show Consent Dialog")]
        private async void ShowConsentDialog()
        {
            if (!RequireInitialized()) return;
            var accepted = await _ads.ShowConsentDialogAsync();
            Debug.Log($"[AdsDemo] ShowConsentDialogAsync result: {accepted}");
        }

        private bool RequireInitialized()
        {
            if (_ads != null && _ads.State == AdsInitState.Initialized) return true;
            Debug.LogWarning("[AdsDemo] Not initialized yet — wait for the 'Initialized.' log after pressing Play.");
            return false;
        }

        /// <summary>Logs every paid impression instead of sending it anywhere — see also the
        /// IntegrationExamples sample's FirebaseAdRevenueReporterExample for a real backend shape.</summary>
        private sealed class LoggingRevenueReporter : IAdRevenueReporter
        {
            public void OnAdRevenue(AdRevenueInfo info) =>
                Debug.Log($"[AdsDemo] RevenuePaid: format={info.Format} network={info.NetworkName} " +
                          $"adUnit={info.AdUnitId} revenue={info.Revenue} {info.Currency} placement={info.Placement}");
        }
    }
}
