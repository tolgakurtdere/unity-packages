using System.Threading.Tasks;
using NUnit.Framework;
using TK.Analytics;

namespace TK.Analytics.Tests
{
    [TestFixture]
    public sealed class FakeAnalyticsBackendTests
    {
        // 1
        [Test]
        public async Task Initialize_CountsAndReadyTask()
        {
            var fake = new FakeAnalyticsBackend();

            await fake.InitializeAsync();

            Assert.AreEqual(1, fake.InitializeCalls);
        }

        // 2
        [Test]
        public void Initialize_ThrowKnob_Throws()
        {
            var fake = new FakeAnalyticsBackend { ThrowOnInit = true };

            Assert.ThrowsAsync<System.InvalidOperationException>(
                async () => await fake.InitializeAsync());
        }

        // 3
        [Test]
        public void LogEvent_RecordsAndTraces()
        {
            var fake = new FakeAnalyticsBackend();

            fake.LogEvent(new AnalyticsEvent("e"));

            Assert.AreEqual(1, fake.Events.Count);
            Assert.AreEqual("event:e", fake.Trace[0]);

            fake.ThrowOnLogEvent = true;
            Assert.Throws<System.InvalidOperationException>(
                () => fake.LogEvent(new AnalyticsEvent("e2")));
        }

        // 4
        [Test]
        public void RevenueAndUser_Recorded()
        {
            var fake = new FakeAnalyticsBackend();

            fake.LogPurchase(new AnalyticsPurchase("com.game.gold", 0.99, "USD", "txn-1"));
            fake.LogAdRevenue(new AnalyticsAdRevenue("rewarded", "admob", "unit-1", 0.01, "USD", null));
            fake.SetUserProperty("k", "v");
            fake.SetUserId("u");
            fake.Flush();

            Assert.AreEqual(1, fake.Purchases.Count);
            Assert.AreEqual(1, fake.AdRevenues.Count);
            Assert.AreEqual("v", fake.UserProperties["k"]);
            Assert.AreEqual("u", fake.UserId);
            Assert.AreEqual(1, fake.FlushCalls);
        }
    }
}
