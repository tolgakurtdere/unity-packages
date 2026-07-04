using NUnit.Framework;

namespace TK.Ads.Tests
{
    [TestFixture]
    public sealed class FakeAdsGatewayTests
    {
        [Test]
        public void Initialize_RaisesInitialized()
        {
            var gateway = new FakeAdsGateway();
            var initialized = false;
            gateway.Initialized += () => initialized = true;

            gateway.InitializeAsync().Wait();

            Assert.IsTrue(initialized);
        }

        [Test]
        public void Initialize_FailKnob_RaisesInitializeFailed()
        {
            var gateway = new FakeAdsGateway { FailInit = true };
            string failMessage = null;
            gateway.InitializeFailed += message => failMessage = message;

            gateway.InitializeAsync().Wait();

            Assert.AreEqual("fake: init failed", failMessage);
        }

        [Test]
        public void LoadAndShow_RecordCalls()
        {
            var gateway = new FakeAdsGateway();

            gateway.LoadInterstitial("u1");
            gateway.ShowInterstitial("place");

            CollectionAssert.Contains(gateway.LoadInterstitialCalls, "u1");
            CollectionAssert.Contains(gateway.ShowInterstitialPlacements, "place");
        }

        [Test]
        public void DeliverRewardReceived_RaisesEvent()
        {
            var gateway = new FakeAdsGateway();
            var rewardReceived = false;
            gateway.RewardReceived += () => rewardReceived = true;

            gateway.DeliverRewardReceived();

            Assert.IsTrue(rewardReceived);
        }
    }
}
