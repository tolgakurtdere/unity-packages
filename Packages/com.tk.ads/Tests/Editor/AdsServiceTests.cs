using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.Ads.Tests
{
    [TestFixture]
    public sealed class AdsServiceTests
    {
        // ── Helpers ──

        private static AdsSettings MakeSettings(
            string bannerId = "b_unit", string interstitialId = "i_unit", string rewardedId = "r_unit",
            int interstitialMinIntervalSeconds = 60, int cooldownAfterRewardedSeconds = 60)
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            settings.androidBannerAdUnitId = bannerId;
            settings.androidInterstitialAdUnitId = interstitialId;
            settings.androidRewardedAdUnitId = rewardedId;
            settings.interstitialMinIntervalSeconds = interstitialMinIntervalSeconds;
            settings.cooldownAfterRewardedSeconds = cooldownAfterRewardedSeconds;
            return settings;
        }

        private static AdsService NewService(AdsSettings settings, FakeAdsGateway gateway, FakeClock clock, AdsOptions options = null)
        {
            options ??= new AdsOptions();
            options.Gateway = gateway;
            options.Clock = clock.Read;
            options.RetryDelayScale = 0f;
            return new AdsService(settings, options);
        }

        private sealed class RecordingReporter : IAdRevenueReporter
        {
            public readonly List<AdRevenueInfo> Received = new();
            public System.Exception ThrowOnReport;

            public void OnAdRevenue(AdRevenueInfo info)
            {
                Received.Add(info);
                if (ThrowOnReport != null) throw ThrowOnReport;
            }
        }

        private static async Task PollUntil(System.Func<bool> done, int maxYields = 50)
        {
            for (int i = 0; i < maxYields && !done(); i++) await Task.Yield();
        }

        // ── Initialization ──

        [Test]
        public void Initialize_HappyPath_LoadsAllFormats()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            var initializedRaised = 0;
            svc.Initialized += () => initializedRaised++;

            svc.InitializeAsync().Wait();

            Assert.AreEqual(AdsInitState.Initialized, svc.State);
            Assert.AreEqual(1, initializedRaised);
            Assert.AreEqual("b_unit", gateway.CreatedBannerAdUnitId);
            CollectionAssert.AreEqual(new[] { "i_unit" }, gateway.LoadInterstitialCalls);
            CollectionAssert.AreEqual(new[] { "r_unit" }, gateway.LoadRewardedCalls);
        }

        [Test]
        public void Initialize_SingleFlight()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            var initializedRaised = 0;
            svc.Initialized += () => initializedRaised++;

            var task1 = svc.InitializeAsync();
            var task2 = svc.InitializeAsync();
            Task.WaitAll(task1, task2);

            Assert.AreEqual(AdsInitState.Initialized, svc.State);
            Assert.AreEqual(1, initializedRaised, "two overlapping InitializeAsync calls must only drive the gateway init once");
            CollectionAssert.AreEqual(new[] { "i_unit" }, gateway.LoadInterstitialCalls);
        }

        [Test]
        public void Initialize_Fail_StateFailed_LateInitializedIgnored()
        {
            var gateway = new FakeAdsGateway { FailInit = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            var initFailedRaised = 0;
            svc.InitFailed += () => initFailedRaised++;

            LogAssert.Expect(LogType.Error, new Regex("Initialization failed"));
            svc.InitializeAsync().Wait();

            Assert.AreEqual(AdsInitState.Failed, svc.State);
            Assert.AreEqual(1, initFailedRaised);

            // Late race: gateway fires Initialized after we already failed. Must be ignored.
            gateway.DeliverInitialized();

            Assert.AreEqual(AdsInitState.Failed, svc.State, "a late Initialized race must not resurrect a failed init");
        }

        [Test]
        public void Initialize_GatewayThrows_StateFailed()
        {
            var gateway = new FakeAdsGateway { ThrowOnInit = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            var initFailedRaised = 0;
            svc.InitFailed += () => initFailedRaised++;

            LogAssert.Expect(LogType.Exception, new Regex("init exploded"));
            LogAssert.Expect(LogType.Error, new Regex("Initialization failed"));
            svc.InitializeAsync().Wait();

            Assert.AreEqual(AdsInitState.Failed, svc.State);
            Assert.AreEqual(1, initFailedRaised);
        }

        [Test]
        public void PolicyGates_SkipLoads()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var options = new AdsOptions
            {
                ShouldLoadBanner = () => false,
                ShouldLoadInterstitial = () => false
            };
            var svc = NewService(MakeSettings(), gateway, clock, options);

            svc.InitializeAsync().Wait();

            Assert.IsNull(gateway.CreatedBannerAdUnitId, "banner load must be skipped when ShouldLoadBanner is false");
            Assert.AreEqual(0, gateway.LoadInterstitialCalls.Count, "interstitial load must be skipped when ShouldLoadInterstitial is false");
            CollectionAssert.AreEqual(new[] { "r_unit" }, gateway.LoadRewardedCalls, "rewarded must never be policy-gated");
        }

        [Test]
        public void EmptyAdUnit_SkipsFormat()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var settings = MakeSettings(rewardedId: "");
            var svc = NewService(settings, gateway, clock);

            Assert.DoesNotThrow(() => svc.InitializeAsync().Wait());

            Assert.AreEqual(AdsInitState.Initialized, svc.State);
            Assert.AreEqual(0, gateway.LoadRewardedCalls.Count, "empty rewarded ad unit id must skip the load, not crash");
        }

        // ── Banner ──

        [Test]
        public void Banner_ShowBeforeLoad_AutoShowsOnLoad()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            svc.ShowBanner();
            Assert.AreEqual(0, gateway.ShowBannerCalls, "banner has not loaded yet, so no gateway.ShowBanner() call should have happened");

            gateway.DeliverBannerLoaded();

            Assert.AreEqual(1, gateway.ShowBannerCalls);
            Assert.IsTrue(svc.IsBannerVisible);
        }

        [Test]
        public void Banner_PolicyBlocked_NoShow()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var options = new AdsOptions { ShouldShowBanner = () => false };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();

            svc.ShowBanner();
            gateway.DeliverBannerLoaded();

            Assert.AreEqual(0, gateway.ShowBannerCalls, "ShouldShowBanner=false must block the show even after load");
            Assert.IsFalse(svc.IsBannerVisible);
        }

        [Test]
        public void Banner_HideThenLoad_NoAutoShow()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            svc.ShowBanner();
            svc.HideBanner();
            gateway.DeliverBannerLoaded();

            Assert.AreEqual(0, gateway.ShowBannerCalls, "hiding before load must cancel the show intent");
            Assert.IsFalse(svc.IsBannerVisible);
        }

        [Test]
        public async Task Banner_LoadFailed_RetriesRecreate()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();
            Assert.AreEqual(1, gateway.CreateBannerCalls, "sanity: initial create happened during init");

            LogAssert.Expect(LogType.Warning, new Regex("Banner load failed"));
            gateway.DeliverBannerLoadFailed("net error");

            await PollUntil(() => gateway.CreateBannerCalls >= 2);

            Assert.GreaterOrEqual(gateway.CreateBannerCalls, 2, "a failed banner load must retry by recreating the banner");
        }

        [Test]
        public async Task Banner_DestroyCancelsRetry()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();
            var createCallsAfterInit = gateway.CreateBannerCalls;

            LogAssert.Expect(LogType.Warning, new Regex("Banner load failed"));
            gateway.DeliverBannerLoadFailed("net error");
            svc.DestroyBanner();

            for (int i = 0; i < 10; i++) await Task.Yield();

            Assert.AreEqual(createCallsAfterInit, gateway.CreateBannerCalls,
                "destroying the banner must cancel the pending retry-recreate");
        }

        // ── Interstitial ──

        [Test]
        public void Interstitial_HappyPath_TrueAndPaced()
        {
            var gateway = new FakeAdsGateway { InterstitialReady = true };
            var clock = new FakeClock();
            var muteCalls = new List<bool>();
            var options = new AdsOptions { AudioMuteSetter = muteCalls.Add };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();
            var loadCallsBeforeShow = gateway.LoadInterstitialCalls.Count;
            var closedRaised = 0;
            svc.InterstitialClosed += () => closedRaised++;

            var showTask = svc.ShowInterstitialAsync("src");
            Assert.AreEqual(new[] { "src" }, gateway.ShowInterstitialPlacements.ToArray(), "placement must be forwarded to the gateway");

            gateway.DeliverInterstitialDisplayed();
            gateway.DeliverInterstitialHidden();

            Assert.IsTrue(showTask.IsCompleted);
            Assert.IsTrue(showTask.Result, "a full happy-path show must resolve true");
            Assert.AreEqual(1, closedRaised);
            Assert.Greater(gateway.LoadInterstitialCalls.Count, loadCallsBeforeShow, "closing must trigger a reload");
            CollectionAssert.AreEqual(new[] { true, false }, muteCalls, "audio mute must be balanced: muted on show, unmuted on finish");

            // Pacer now blocks: immediate second call is denied (guard early-return -> already-completed task).
            gateway.InterstitialReady = true;
            var secondTask = svc.ShowInterstitialAsync();
            Assert.IsFalse(secondTask.Result, "pacer must block an immediate second interstitial");
            Assert.AreEqual(1, gateway.ShowInterstitialPlacements.Count, "a paced-out call must never reach the gateway's show");

            // After the interval elapses, pacing allows a third show to actually start (its task
            // resolves only once Hidden fires, so we assert on dispatch, not on task completion).
            clock.Advance(60);
            var thirdTask = svc.ShowInterstitialAsync();
            Assert.IsFalse(thirdTask.IsCompleted, "an allowed show must be in-flight, awaiting the gateway's Hidden callback");
            Assert.AreEqual(2, gateway.ShowInterstitialPlacements.Count, "after the pacing interval elapses, the gateway must be asked to show again");

            gateway.DeliverInterstitialDisplayed();
            gateway.DeliverInterstitialHidden();
            Assert.IsTrue(thirdTask.Result, "the third show must resolve true once hidden is delivered");
        }

        [Test]
        public void Interstitial_NotReady_False_NoMute()
        {
            var gateway = new FakeAdsGateway { InterstitialReady = false };
            var clock = new FakeClock();
            var muteCalls = new List<bool>();
            var options = new AdsOptions { AudioMuteSetter = muteCalls.Add };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();

            var task = svc.ShowInterstitialAsync();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.Result);
            Assert.AreEqual(0, gateway.ShowInterstitialPlacements.Count, "gateway must never be asked to show when not ready");
            Assert.AreEqual(0, muteCalls.Count, "audio must not be touched when the show never actually starts");
        }

        [Test]
        public void Interstitial_PacingBlocks_False()
        {
            var gateway = new FakeAdsGateway { InterstitialReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            var firstTask = svc.ShowInterstitialAsync();
            gateway.DeliverInterstitialDisplayed();
            gateway.DeliverInterstitialHidden();
            Assert.IsTrue(firstTask.Result, "sanity: the first interstitial must have shown successfully");
            var showCallsAfterFirst = gateway.ShowInterstitialPlacements.Count;

            gateway.InterstitialReady = true;
            var blocked = svc.ShowInterstitialAsync();

            Assert.IsTrue(blocked.Result == false, "before the pacing interval elapses, showing must be blocked");
            Assert.AreEqual(showCallsAfterFirst, gateway.ShowInterstitialPlacements.Count, "a paced-out call must never reach the gateway's show");
        }

        [Test]
        public void Interstitial_CooldownAfterRewarded_Blocks()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true, InterstitialReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            svc.ShowRewardedAsync();
            gateway.DeliverRewardReceived();
            gateway.DeliverRewardedHidden();

            var blocked = svc.ShowInterstitialAsync();
            Assert.IsFalse(blocked.Result, "an interstitial must be blocked immediately after a completed rewarded ad");

            clock.Advance(60);
            var allowed = svc.ShowInterstitialAsync();
            Assert.IsFalse(allowed.IsCompleted, "after the cooldown elapses, showing must actually start (task pends on the gateway's Hidden callback)");
            Assert.AreEqual(1, gateway.ShowInterstitialPlacements.Count, "the gateway must have been asked to show once the cooldown lifted");

            gateway.DeliverInterstitialDisplayed();
            gateway.DeliverInterstitialHidden();
            Assert.IsTrue(allowed.Result, "after the cooldown elapses, the interstitial must be allowed");
        }

        [Test]
        public void Interstitial_DisplayFailed_FalseAndMuteBalanced()
        {
            var gateway = new FakeAdsGateway { InterstitialReady = true };
            var clock = new FakeClock();
            var muteCalls = new List<bool>();
            var options = new AdsOptions { AudioMuteSetter = muteCalls.Add };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();
            var loadCallsBeforeShow = gateway.LoadInterstitialCalls.Count;

            var task = svc.ShowInterstitialAsync();

            LogAssert.Expect(LogType.Warning, new Regex("Interstitial display failed"));
            gateway.DeliverInterstitialDisplayFailed("no fill");

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.Result, "a display failure must resolve the show task to false");
            Assert.Greater(gateway.LoadInterstitialCalls.Count, loadCallsBeforeShow, "a display failure must still trigger a reload");
            CollectionAssert.AreEqual(new[] { true, false }, muteCalls, "audio mute must be balanced even on the display-failed path");
        }

        // ── Rewarded ──

        [Test]
        public void Rewarded_RewardThenHide_Rewarded()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true, InterstitialReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();
            var loadCallsBeforeShow = gateway.LoadRewardedCalls.Count;
            var readyChangedRaised = 0;
            svc.RewardedReadyChanged += () => readyChangedRaised++;

            var task = svc.ShowRewardedAsync("src");
            gateway.DeliverRewardReceived();
            gateway.DeliverRewardedHidden();

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(RewardedResult.Rewarded, task.Result);
            Assert.Greater(gateway.LoadRewardedCalls.Count, loadCallsBeforeShow, "completing a rewarded ad must trigger a reload");
            Assert.AreEqual(1, readyChangedRaised);

            // Cooldown must now be active: an interstitial is blocked.
            var blocked = svc.ShowInterstitialAsync();
            Assert.IsFalse(blocked.Result, "a completed rewarded ad must set the interstitial cooldown");
        }

        [Test]
        public void Rewarded_HideWithoutReward_Cancelled_NoCooldown()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true, InterstitialReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            var task = svc.ShowRewardedAsync();
            gateway.DeliverRewardedHidden(); // no RewardReceived before hidden

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(RewardedResult.Cancelled, task.Result);

            var allowed = svc.ShowInterstitialAsync();
            Assert.IsFalse(allowed.IsCompleted, "an interstitial must actually start (not be cooldown-blocked) after a cancelled rewarded ad");
            Assert.AreEqual(1, gateway.ShowInterstitialPlacements.Count, "a cancelled (non-rewarded) rewarded ad must NOT set the interstitial cooldown");

            gateway.DeliverInterstitialDisplayed();
            gateway.DeliverInterstitialHidden();
            Assert.IsTrue(allowed.Result);
        }

        [Test]
        public void Rewarded_DisplayFailed_FailedToShow()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            var task = svc.ShowRewardedAsync();

            LogAssert.Expect(LogType.Warning, new Regex("Rewarded display failed"));
            gateway.DeliverRewardedDisplayFailed("no fill");

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(RewardedResult.FailedToShow, task.Result);
        }

        [Test]
        public void Rewarded_MuteBalanced_OnHappyPath()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true };
            var clock = new FakeClock();
            var muteCalls = new List<bool>();
            var options = new AdsOptions { AudioMuteSetter = muteCalls.Add };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();

            var task = svc.ShowRewardedAsync();
            gateway.DeliverRewardReceived();
            gateway.DeliverRewardedHidden();

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(RewardedResult.Rewarded, task.Result);
            CollectionAssert.AreEqual(new[] { true, false }, muteCalls, "audio mute must be balanced: muted on show, unmuted once the rewarded ad finishes");
        }

        [Test]
        public void Rewarded_MuteBalanced_OnDisplayFailed()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true };
            var clock = new FakeClock();
            var muteCalls = new List<bool>();
            var options = new AdsOptions { AudioMuteSetter = muteCalls.Add };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();

            var task = svc.ShowRewardedAsync();

            LogAssert.Expect(LogType.Warning, new Regex("Rewarded display failed"));
            gateway.DeliverRewardedDisplayFailed("no fill");

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(RewardedResult.FailedToShow, task.Result);
            CollectionAssert.AreEqual(new[] { true, false }, muteCalls, "audio mute must be balanced even on the rewarded display-failed path");
        }

        [Test]
        public void Rewarded_NotReady_And_NotInitialized_Results()
        {
            var gateway = new FakeAdsGateway { RewardedReady = false };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);

            var beforeInit = svc.ShowRewardedAsync();
            Assert.AreEqual(RewardedResult.NotInitialized, beforeInit.Result, "before initialization, rewarded must resolve NotInitialized");

            svc.InitializeAsync().Wait();
            var notReady = svc.ShowRewardedAsync();
            Assert.AreEqual(RewardedResult.NotReady, notReady.Result, "once initialized but with no ad loaded, rewarded must resolve NotReady");
        }

        // ── Cross-cutting ──

        [Test]
        public void DoubleShow_Guard()
        {
            var gateway = new FakeAdsGateway { RewardedReady = true, InterstitialReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            var rewardedTask = svc.ShowRewardedAsync();
            gateway.DeliverRewardedDisplayed(); // fullscreen ad is in progress; not yet Hidden

            var interstitialDuring = svc.ShowInterstitialAsync();
            var rewardedDuring = svc.ShowRewardedAsync();

            Assert.IsFalse(interstitialDuring.Result, "an interstitial must be refused while a fullscreen ad is already in progress");
            Assert.AreEqual(RewardedResult.NotReady, rewardedDuring.Result, "a second rewarded request must be refused while one is already in progress");

            Assert.IsFalse(rewardedTask.IsCompleted, "the original in-flight rewarded task must still be pending");
        }

        [Test]
        public void Revenue_ReporterCalled_AndExceptionTolerated()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var reporter = new RecordingReporter();
            var options = new AdsOptions { RevenueReporter = reporter };
            var svc = NewService(MakeSettings(), gateway, clock, options);
            svc.InitializeAsync().Wait();

            var info = new AdRevenueInfo("interstitial", "network_x", "i_unit", 0.0123, "USD", "src");
            gateway.DeliverRevenue(info);

            Assert.AreEqual(1, reporter.Received.Count);
            Assert.AreEqual(info.Format, reporter.Received[0].Format);
            Assert.AreEqual(info.NetworkName, reporter.Received[0].NetworkName);
            Assert.AreEqual(info.AdUnitId, reporter.Received[0].AdUnitId);
            Assert.AreEqual(info.Revenue, reporter.Received[0].Revenue);
            Assert.AreEqual(info.Currency, reporter.Received[0].Currency);
            Assert.AreEqual(info.Placement, reporter.Received[0].Placement);

            reporter.ThrowOnReport = new System.InvalidOperationException("reporter boom");
            LogAssert.Expect(LogType.Exception, new Regex("reporter boom"));
            Assert.DoesNotThrow(() => gateway.DeliverRevenue(info));

            Assert.AreEqual(2, reporter.Received.Count, "flow must continue and still record the call even though the reporter threw");
        }
    }
}
