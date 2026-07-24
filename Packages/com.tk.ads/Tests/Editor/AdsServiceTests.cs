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

        private static AdsService NewService(AdsSettings settings, RecordingAdsGateway gateway, FakeClock clock, AdsOptions options = null)
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway { FailInit = true };
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
            var gateway = new RecordingAdsGateway { ThrowOnInit = true };
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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
            var gateway = new RecordingAdsGateway();
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

        [Test]
        public void Banner_DestroyThenShow_RecreatesAndShows()
        {
            var gateway = new RecordingAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            // First show: banner created during init, loads, and shows.
            svc.ShowBanner();
            gateway.DeliverBannerLoaded();
            Assert.AreEqual(1, gateway.CreateBannerCalls, "sanity: banner created once during init");
            Assert.AreEqual(1, gateway.ShowBannerCalls, "sanity: banner shows once it loads");
            Assert.IsTrue(svc.IsBannerVisible);

            // Destroy frees the banner.
            svc.DestroyBanner();
            Assert.AreEqual(1, gateway.DestroyBannerCalls, "DestroyBanner must reach the gateway");
            Assert.IsFalse(svc.IsBannerVisible, "banner must not be visible after destroy");

            // Reversible: a later ShowBanner re-creates it (intent-based, same as first-time show).
            svc.ShowBanner();
            Assert.AreEqual(2, gateway.CreateBannerCalls, "ShowBanner after DestroyBanner must re-create the banner");
            Assert.AreEqual(1, gateway.ShowBannerCalls, "nothing is loaded yet, so no direct ShowBanner call in the recreate path");
            Assert.IsFalse(svc.IsBannerVisible, "banner is not visible until the recreated banner loads");

            // Once the recreated banner loads it auto-shows again.
            gateway.DeliverBannerLoaded();
            Assert.AreEqual(2, gateway.ShowBannerCalls, "the recreated banner must auto-show on load");
            Assert.IsTrue(svc.IsBannerVisible);
        }

        // ── Interstitial ──

        [Test]
        public void Interstitial_HappyPath_TrueAndPaced()
        {
            var gateway = new RecordingAdsGateway { InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway { InterstitialReady = false };
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
            var gateway = new RecordingAdsGateway { InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true, InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway { InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true, InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true, InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true };
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
        public void Rewarded_ReentrantShowFromContinuation_DoesNotHang()
        {
            // Regression guard for the TCS complete-after-clear idiom. A continuation on the rewarded
            // task synchronously re-enters ShowRewardedAsync. Because TCS continuations run
            // synchronously, the OLD "TrySetResult then null the field" order would null the fresh TCS
            // installed by the re-entrant call, hanging its task forever. With the field nulled BEFORE
            // completion, the re-entrant call's TCS survives and the second task is properly tracked.
            var gateway = new RecordingAdsGateway { RewardedReady = true }; // stays ready after the first (synchronous re-arm)
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            Task<RewardedResult> secondTask = null;
            var first = svc.ShowRewardedAsync();
            // Synchronous continuation: fires inside OnRewardedHidden's TrySetResult, re-entering Show.
            first.ContinueWith(_ => { secondTask = svc.ShowRewardedAsync(); },
                TaskContinuationOptions.ExecuteSynchronously);

            gateway.DeliverRewardedHidden(); // completes `first`, whose continuation re-enters Show

            Assert.IsTrue(first.IsCompleted, "sanity: the first rewarded task must have completed");
            Assert.IsNotNull(secondTask, "the re-entrant continuation must have started a second show");

            // The point of the test: the second task must be alive and completable, not hung.
            // It is in-flight (a fresh TCS was installed and survived), so its Hidden completes it.
            Assert.IsFalse(secondTask.IsCompleted, "the re-entrant show must be in-flight, awaiting its own Hidden");
            gateway.DeliverRewardedHidden();
            Assert.IsTrue(secondTask.IsCompleted, "the re-entrant show's task must complete (not hang) when its Hidden fires");
            Assert.AreEqual(RewardedResult.Cancelled, secondTask.Result, "no reward was latched on the second show");
        }

        [Test]
        public void Rewarded_SequentialShows_LatchResetsCleanly()
        {
            // Pins the reward-latch reset across sequential shows: a rewarded first show followed by a
            // cancelled second show must NOT carry the first show's reward into the second.
            var gateway = new RecordingAdsGateway { RewardedReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            var first = svc.ShowRewardedAsync();
            gateway.DeliverRewardReceived();
            gateway.DeliverRewardedHidden();
            Assert.AreEqual(RewardedResult.Rewarded, first.Result, "first show earned the reward");

            var second = svc.ShowRewardedAsync();
            gateway.DeliverRewardedHidden(); // no RewardReceived this time
            Assert.AreEqual(RewardedResult.Cancelled, second.Result,
                "the latch must reset between shows: a second show without a reward must be Cancelled, not Rewarded");
        }

        [Test]
        public void ConsentDialog_FormNotRequired_ReportsSuccess()
        {
            // The gateway maps MaxCmpError.ErrorCode.FormNotRequired -> success (review-verified against
            // the installed MAX 8.6.2 source). At the service level we can only prove the service
            // forwards a success outcome verbatim; the fake models the already-mapped result.
            var gateway = new RecordingAdsGateway { ConsentDialogResult = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();

            var result = svc.ShowConsentDialogAsync().Result;

            Assert.IsTrue(result, "a FormNotRequired (already-resolved) consent outcome must surface as success");
            Assert.AreEqual(1, gateway.ConsentDialogCalls);
        }

        [Test]
        public void Events_AfterTeardown_StillBenign()
        {
            var gateway = new RecordingAdsGateway { RewardedReady = true, InterstitialReady = true };
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            svc.InitializeAsync().Wait();
            svc.Teardown();

            // Late gateway callbacks after teardown must not crash the service.
            Assert.DoesNotThrow(() =>
            {
                gateway.DeliverInterstitialHidden();
                gateway.DeliverRewardedHidden();
                gateway.DeliverBannerLoaded();
            }, "gateway callbacks delivered after Teardown() must be benign");
        }

        [Test]
        public void Rewarded_NotReady_And_NotInitialized_Results()
        {
            var gateway = new RecordingAdsGateway { RewardedReady = false };
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
            var gateway = new RecordingAdsGateway { RewardedReady = true, InterstitialReady = true };
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
            var gateway = new RecordingAdsGateway();
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
