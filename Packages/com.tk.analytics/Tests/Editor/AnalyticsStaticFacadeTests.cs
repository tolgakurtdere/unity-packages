using System.Threading.Tasks;
using NUnit.Framework;
using TK.Analytics;

namespace TK.Analytics.Tests
{
    [TestFixture]
    public sealed class AnalyticsStaticFacadeTests
    {
        [SetUp]
        public void SetUp() => Analytics.ClearInstance();

        [TearDown]
        public void TearDown() => Analytics.ClearInstance();

        /// <summary>Builds a dispatch-ready service so log verbs reach the backend live (not buffered).</summary>
        private static async Task<(AnalyticsService svc, FakeAnalyticsBackend backend)> BuildReadyServiceAsync()
        {
            var backend = new FakeAnalyticsBackend();
            var svc = new AnalyticsService(new[] { backend });
            svc.SetConsent(true);
            await svc.StartAsync();
            return (svc, backend);
        }

        // 1
        [Test]
        public void NoInstance_LogEvent_NoOp_NoThrow()
        {
            Analytics.ClearInstance();

            Assert.DoesNotThrow(() => Analytics.LogEvent("e"));
            Assert.IsFalse(Analytics.HasInstance);
        }

        // 2
        [Test]
        public async Task SetInstance_DelegatesLogEvent()
        {
            var (svc, backend) = await BuildReadyServiceAsync();
            Analytics.SetInstance(svc);

            Analytics.LogEvent("e", AnalyticsParam.Long("n", 1));

            Assert.AreEqual(1, backend.Events.Count);
            Assert.AreEqual("e", backend.Events[0].Name);
            Assert.AreEqual(1, backend.Events[0].Parameters.Count);
            Assert.AreEqual("n", backend.Events[0].Parameters[0].Key);
            Assert.AreEqual(AnalyticsParamType.Long, backend.Events[0].Parameters[0].Type);
            Assert.AreEqual(1L, backend.Events[0].Parameters[0].LongValue);
        }

        // 3
        [Test]
        public async Task SetInstance_DelegatesRevenueAndUser()
        {
            var (svc, backend) = await BuildReadyServiceAsync();
            Analytics.SetInstance(svc);

            Analytics.LogPurchase(new AnalyticsPurchase("com.game.gold", 0.99, "USD", "txn-1"));
            Analytics.LogAdRevenue(new AnalyticsAdRevenue("rewarded", "admob", "unit-1", 0.01, "USD", null));
            Analytics.SetUserProperty("k", "v");
            Analytics.SetUserId("u");

            Assert.AreEqual(1, backend.Purchases.Count);
            Assert.AreEqual("com.game.gold", backend.Purchases[0].ProductId);
            Assert.AreEqual(1, backend.AdRevenues.Count);
            Assert.AreEqual("unit-1", backend.AdRevenues[0].AdUnitId);
            Assert.AreEqual("v", backend.UserProperties["k"]);
            Assert.AreEqual("u", backend.UserId);
        }

        // 4
        [Test]
        public async Task HasInstance_Reflects_SetAndClear()
        {
            var (svc, _) = await BuildReadyServiceAsync();

            Analytics.ClearInstance();
            Assert.IsFalse(Analytics.HasInstance);

            Analytics.SetInstance(svc);
            Assert.IsTrue(Analytics.HasInstance);

            Analytics.ClearInstance();
            Assert.IsFalse(Analytics.HasInstance);
        }

        // 5
        [Test]
        public async Task ClearInstance_StopsDelegation()
        {
            var (svc, backend) = await BuildReadyServiceAsync();
            Analytics.SetInstance(svc);

            Analytics.LogEvent("e");
            Assert.AreEqual(1, backend.Events.Count);

            Analytics.ClearInstance();
            Analytics.LogEvent("e2");

            Assert.AreEqual(1, backend.Events.Count);
        }
    }
}
