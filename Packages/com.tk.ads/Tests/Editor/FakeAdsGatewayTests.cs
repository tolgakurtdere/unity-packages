using System.Collections.Generic;
using NUnit.Framework;
using TK.Ads;
using UnityEngine;

namespace TK.Ads.Tests
{
    /// <summary>
    /// Covers the RUNTIME <see cref="FakeAdsGateway"/> (the official consumer test gateway) —
    /// not the tests' own RecordingAdsGateway probe.
    /// </summary>
    [TestFixture]
    public class FakeAdsGatewayTests
    {
        private FakeAdsGateway _gateway;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeAdsGateway();
            _events = new List<string>();
        }

        private void RecordAll()
        {
            _gateway.Initialized += () => _events.Add("Initialized");
            _gateway.InitializeFailed += m => _events.Add($"InitializeFailed:{m}");
            _gateway.BannerLoaded += () => _events.Add("BannerLoaded");
            _gateway.BannerLoadFailed += m => _events.Add($"BannerLoadFailed:{m}");
            _gateway.InterstitialLoaded += () => _events.Add("InterstitialLoaded");
            _gateway.InterstitialLoadFailed += m => _events.Add($"InterstitialLoadFailed:{m}");
            _gateway.InterstitialDisplayed += () => _events.Add("InterstitialDisplayed");
            _gateway.InterstitialHidden += () => _events.Add("InterstitialHidden");
            _gateway.RewardedLoaded += () => _events.Add("RewardedLoaded");
            _gateway.RewardedLoadFailed += m => _events.Add($"RewardedLoadFailed:{m}");
            _gateway.RewardedDisplayed += () => _events.Add("RewardedDisplayed");
            _gateway.RewardedHidden += () => _events.Add("RewardedHidden");
            _gateway.RewardReceived += () => _events.Add("RewardReceived");
        }

        [Test]
        public void AutoMode_RewardedFullCycle_OrderedAndUnattended()
        {
            RecordAll();

            _gateway.LoadRewarded("unit");
            Assert.IsTrue(_gateway.IsRewardedReady);

            _gateway.ShowRewarded("placement");

            Assert.AreEqual(
                new[] { "RewardedLoaded", "RewardedDisplayed", "RewardReceived", "RewardedHidden" },
                _events.ToArray(),
                "Auto mode must resolve the whole show synchronously, reward before hide.");
            Assert.IsFalse(_gateway.IsRewardedReady, "A shown ad is consumed.");
        }

        [Test]
        public void AutoMode_InterstitialFullCycle_OrderedAndUnattended()
        {
            RecordAll();

            _gateway.LoadInterstitial("unit");
            Assert.IsTrue(_gateway.IsInterstitialReady);

            _gateway.ShowInterstitial("placement");

            Assert.AreEqual(
                new[] { "InterstitialLoaded", "InterstitialDisplayed", "InterstitialHidden" },
                _events.ToArray());
            Assert.IsFalse(_gateway.IsInterstitialReady);
        }

        [Test]
        public void ManualMode_ShowWaitsForResolution()
        {
            RecordAll();
            _gateway.AutoResolveShows = false;

            _gateway.LoadRewarded("unit");
            _gateway.ShowRewarded("placement");

            Assert.AreEqual(new[] { "RewardedLoaded", "RewardedDisplayed" }, _events.ToArray(),
                "Manual mode must stop at Displayed.");

            _gateway.CompleteRewarded();

            Assert.AreEqual(new[] { "RewardedLoaded", "RewardedDisplayed", "RewardReceived", "RewardedHidden" },
                _events.ToArray());
        }

        [Test]
        public void ManualMode_CancelRewarded_HidesWithoutReward()
        {
            RecordAll();
            _gateway.AutoResolveShows = false;

            _gateway.LoadRewarded("unit");
            _gateway.ShowRewarded("placement");
            _gateway.CancelRewarded();

            CollectionAssert.DoesNotContain(_events, "RewardReceived");
            CollectionAssert.Contains(_events, "RewardedHidden");
        }

        [Test]
        public void NoFill_Interstitial_FailsLoad_AndStaysNotReady()
        {
            RecordAll();
            _gateway.InterstitialFill = false;

            _gateway.LoadInterstitial("unit");

            Assert.AreEqual(new[] { "InterstitialLoadFailed:fake: no fill" }, _events.ToArray());
            Assert.IsFalse(_gateway.IsInterstitialReady);
        }

        [Test]
        public void NoFill_Rewarded_FailsLoad_AndStaysNotReady()
        {
            RecordAll();
            _gateway.RewardedFill = false;

            _gateway.LoadRewarded("unit");

            Assert.AreEqual(new[] { "RewardedLoadFailed:fake: no fill" }, _events.ToArray());
            Assert.IsFalse(_gateway.IsRewardedReady);
        }

        [Test]
        public void NoFill_Banner_FailsCreate()
        {
            RecordAll();
            _gateway.BannerFill = false;

            _gateway.CreateBanner("unit", AdsBannerPosition.BottomCenter, Color.black);

            Assert.AreEqual(new[] { "BannerLoadFailed:fake: no fill" }, _events.ToArray());
        }

        [Test]
        public void FailInit_RaisesInitializeFailed_NotInitialized()
        {
            RecordAll();
            _gateway.FailInit = true;

            _gateway.InitializeAsync();

            Assert.AreEqual(new[] { "InitializeFailed:fake: init failed" }, _events.ToArray());
        }

        [Test]
        public void RaiseRevenue_ForwardsInfoToRevenuePaid()
        {
            AdRevenueInfo received = default;
            _gateway.RevenuePaid += info => received = info;
            var sent = new AdRevenueInfo("rewarded", "net", "unit", 0.5, "USD", "placement");

            _gateway.RaiseRevenue(sent);

            Assert.AreEqual(sent, received);
        }

        [Test]
        public void ConsentDialog_HonorsConfiguredResult()
        {
            _gateway.ConsentDialogResult = false;

            var task = _gateway.ShowConsentDialogAsync();

            Assert.IsTrue(task.IsCompleted, "The fake must complete synchronously.");
            Assert.IsFalse(task.Result);
        }
    }
}
