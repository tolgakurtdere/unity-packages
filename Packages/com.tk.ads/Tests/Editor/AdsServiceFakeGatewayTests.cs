using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace TK.Ads.Tests
{
    /// <summary>
    /// Integration coverage for <see cref="AdsService"/> driven by the RUNTIME
    /// <see cref="FakeAdsGateway"/> in its default auto-resolve mode — the exact wiring a consumer
    /// gets when they pass <c>new FakeAdsGateway()</c> as <c>AdsOptions.Gateway</c> for editor/demo
    /// builds. Auto mode resolves an entire show (Displayed, then RewardReceived for rewarded, then
    /// Hidden) SYNCHRONOUSLY inside the gateway's Show call — unlike the hand-rolled
    /// RecordingAdsGateway used by AdsServiceTests.cs, which only ever completes a show when a test
    /// explicitly calls one of its DeliverX methods on a later line. That difference is exactly what
    /// let a service-side bug slip past AdsServiceTests.cs: ShowInterstitialAsync/ShowRewardedAsync
    /// used to re-read the TCS field to build the returned Task, but a synchronous gateway already
    /// nulls that field (the deliberate complete-after-clear idiom, see OnInterstitialHidden) before
    /// control returns to the Show method — an NRE on every single show. These tests pin the fix
    /// (capture the TCS in a local before calling the gateway; return the local, not the field).
    /// </summary>
    [TestFixture]
    public sealed class AdsServiceFakeGatewayTests
    {
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

        [Test]
        public async Task Interstitial_FullCycle_AutoResolvingGateway_ReturnsTrue_AndReloads()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            var interstitialLoadedRaised = 0;
            gateway.InterstitialLoaded += () => interstitialLoadedRaised++;
            var closedRaised = 0;
            svc.InterstitialClosed += () => closedRaised++;

            await svc.InitializeAsync();
            Assert.IsTrue(svc.IsInterstitialReady, "sanity: FakeAdsGateway auto-fills the initial load during init");
            Assert.AreEqual(1, interstitialLoadedRaised, "sanity: one load happened during init");

            // Auto mode resolves the whole show (Displayed -> Hidden) synchronously inside ShowInterstitial,
            // so this task must already be complete by the time ShowInterstitialAsync returns — no frames.
            var result = await svc.ShowInterstitialAsync("p");

            Assert.IsTrue(result, "a full auto-resolved show must return true, not NRE on the TCS re-read");
            Assert.AreEqual(1, closedRaised, "InterstitialClosed must fire once for the completed show");
            Assert.AreEqual(2, interstitialLoadedRaised, "a reload must have been requested after Hidden");
            Assert.IsTrue(svc.IsInterstitialReady, "the reloaded interstitial must be ready again (not stuck mid-show)");
        }

        [Test]
        public async Task Rewarded_FullCycle_AutoResolvingGateway_ReturnsRewarded_MuteBalanced()
        {
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var muteCalls = new List<bool>();
            var options = new AdsOptions { AudioMuteSetter = muteCalls.Add };
            var svc = NewService(MakeSettings(), gateway, clock, options);

            await svc.InitializeAsync();
            Assert.IsTrue(svc.IsRewardedReady, "sanity: FakeAdsGateway auto-fills the initial rewarded load during init");

            // Auto mode fires RewardReceived before Hidden, so a full unattended cycle earns the reward.
            var result = await svc.ShowRewardedAsync("p");

            Assert.AreEqual(RewardedResult.Rewarded, result, "reward latched before Hidden must resolve Rewarded, not NRE on the TCS re-read");
            CollectionAssert.AreEqual(new[] { true, false }, muteCalls,
                "audio mute must be balanced: muted on show, unmuted once the rewarded ad resolves");
        }

        [Test]
        public async Task Interstitial_SecondConsecutiveShow_AlsoResolves()
        {
            // Proves the TCS lifecycle survives synchronous resolution repeatedly, not just once —
            // a fix that only happened to work for the first call (e.g. by accident of field timing)
            // would still be broken for every show after it.
            var gateway = new FakeAdsGateway();
            var clock = new FakeClock();
            var svc = NewService(MakeSettings(), gateway, clock);
            await svc.InitializeAsync();

            var first = await svc.ShowInterstitialAsync("p1");
            Assert.IsTrue(first, "first show must resolve true");

            clock.Advance(60); // clear the default 60s interstitial pacing interval (see MakeSettings)
            Assert.IsTrue(svc.IsInterstitialReady, "sanity: the reloaded interstitial must be ready for a second show");

            var second = await svc.ShowInterstitialAsync("p2");
            Assert.IsTrue(second, "the second show must also resolve true, not hang or NRE");
        }
    }
}
