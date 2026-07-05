using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Analytics;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.Analytics.Tests
{
    [TestFixture]
    public sealed class AnalyticsServiceTests
    {
        private static AnalyticsService NewService(params FakeAnalyticsBackend[] backends)
            => new(backends);

        // 1
        [Test]
        public void Event_BeforeStart_Buffered_NotDispatched()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            service.LogEvent("e");

            Assert.AreEqual(0, backend.Events.Count, "not started → buffered, not dispatched");
        }

        // 2
        [Test]
        public async Task Event_AfterStartAndConsent_Dispatched()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            await service.StartAsync();
            service.LogEvent("e");

            Assert.AreEqual(1, backend.Events.Count);
            Assert.AreEqual("e", backend.Events[0].Name);
        }

        // 3 — reference param-loss regression: params must survive buffering.
        [Test]
        public async Task BufferedEvent_FlushedOnStart_WithParamsIntact()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            service.LogEvent("e", AnalyticsParam.Long("n", 5)); // buffered before start

            await service.StartAsync();

            Assert.AreEqual(1, backend.Events.Count);
            Assert.AreEqual(1, backend.Events[0].Parameters.Count);
            Assert.AreEqual("n", backend.Events[0].Parameters[0].Key);
            Assert.AreEqual(AnalyticsParamType.Long, backend.Events[0].Parameters[0].Type);
            Assert.AreEqual(5, backend.Events[0].Parameters[0].LongValue);
        }

        // 4
        [Test]
        public async Task Event_ConsentUnknown_BufferedEvenAfterStart_ThenFlushedOnGrant()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            await service.StartAsync(); // consent still Unknown
            service.LogEvent("e");
            Assert.AreEqual(0, backend.Events.Count, "consent Unknown → buffered even after start");

            service.SetConsent(true);
            Assert.AreEqual(1, backend.Events.Count, "granting consent flushes the buffer");
        }

        // 5
        [Test]
        public async Task SetConsentFalse_ClearsBuffer_AndDropsFuture()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.LogEvent("a");   // consent Unknown → buffered
            service.SetConsent(false); // clears buffer
            await service.StartAsync();
            service.LogEvent("b");   // Denied → dropped, never sent

            Assert.AreEqual(0, backend.Events.Count, "buffer cleared and future dropped as Denied");
        }

        // 6
        [Test]
        public async Task Disabled_DropsEvent_NotBuffered()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            await service.StartAsync();

            service.IsEnabled = false;
            service.LogEvent("e");
            Assert.AreEqual(0, backend.Events.Count, "disabled → dropped");

            service.IsEnabled = true;
            Assert.AreEqual(0, backend.Events.Count, "re-enabling does not recover a dropped op");
        }

        // 7
        [Test]
        public async Task StartAsync_InitializesAllBackends_Once_SingleFlight()
        {
            var a = new FakeAnalyticsBackend("a");
            var b = new FakeAnalyticsBackend("b");
            var service = NewService(a, b);

            await service.StartAsync();
            Assert.AreEqual(1, a.InitializeCalls);
            Assert.AreEqual(1, b.InitializeCalls);

            await service.StartAsync(); // single-flight: no re-init
            Assert.AreEqual(1, a.InitializeCalls);
            Assert.AreEqual(1, b.InitializeCalls);
        }

        // 8
        [Test]
        public async Task Purchase_BufferedThenDispatched_FieldsIntact()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            service.LogPurchase(new AnalyticsPurchase("p", 1.99, "USD", "tx1")); // buffered

            await service.StartAsync();

            Assert.AreEqual(1, backend.Purchases.Count);
            Assert.AreEqual("p", backend.Purchases[0].ProductId);
            Assert.AreEqual(1.99, backend.Purchases[0].Price, 1e-9);
            Assert.AreEqual("USD", backend.Purchases[0].Currency);
            Assert.AreEqual("tx1", backend.Purchases[0].TransactionId);
            Assert.AreEqual(1, backend.Purchases[0].Quantity);
        }

        // 9
        [Test]
        public async Task AdRevenue_Dispatched_FieldsIntact()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            await service.StartAsync();

            service.LogAdRevenue(new AnalyticsAdRevenue("banner", "admob", "unit1", 0.01, "USD", "main"));

            Assert.AreEqual(1, backend.AdRevenues.Count);
            Assert.AreEqual("banner", backend.AdRevenues[0].Format);
            Assert.AreEqual("admob", backend.AdRevenues[0].AdNetwork);
            Assert.AreEqual("unit1", backend.AdRevenues[0].AdUnitId);
            Assert.AreEqual(0.01, backend.AdRevenues[0].Revenue, 1e-9);
            Assert.AreEqual("USD", backend.AdRevenues[0].Currency);
            Assert.AreEqual("main", backend.AdRevenues[0].Placement);
        }

        // 10
        [Test]
        public async Task UserPropertyAndUserId_Dispatched()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            await service.StartAsync();

            service.SetUserProperty("k", "v");
            service.SetUserId("u");

            Assert.AreEqual("v", backend.UserProperties["k"]);
            Assert.AreEqual("u", backend.UserId);
        }

        // 11
        [Test]
        public async Task Buffer_ReplaysInFifoOrder()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            service.SetUserId("u");   // buffered
            service.LogEvent("e1");   // buffered
            service.LogEvent("e2");   // buffered

            await service.StartAsync();

            CollectionAssert.AreEqual(new[] { "userid:u", "event:e1", "event:e2" }, backend.Trace);
        }

        // 12
        [Test]
        public async Task FanOut_AllBackendsReceive()
        {
            var a = new FakeAnalyticsBackend("a");
            var b = new FakeAnalyticsBackend("b");
            var service = NewService(a, b);

            service.SetConsent(true);
            await service.StartAsync();
            service.LogEvent("e");

            Assert.AreEqual(1, a.Events.Count);
            Assert.AreEqual(1, b.Events.Count);
        }

        // 13
        [Test]
        public async Task BackendThrowsInLogEvent_OthersStillReceive()
        {
            var a = new FakeAnalyticsBackend("fake") { ThrowOnLogEvent = true };
            var b = new FakeAnalyticsBackend("b");
            var service = NewService(a, b);

            service.SetConsent(true);
            await service.StartAsync();

            LogAssert.Expect(LogType.Exception, new Regex("fake: logevent threw"));
            service.LogEvent("e"); // A throws, service catches; B still receives

            Assert.AreEqual(1, b.Events.Count, "a throwing must not block b");
        }

        // 14
        [Test]
        public async Task BackendThrowsInInit_StartCompletes_ServiceUsable()
        {
            var a = new FakeAnalyticsBackend("fake") { ThrowOnInit = true };
            var b = new FakeAnalyticsBackend("b");
            var service = NewService(a, b);

            service.SetConsent(true);

            LogAssert.Expect(LogType.Exception, new Regex("fake: init threw"));
            await service.StartAsync();

            Assert.IsTrue(service.IsStarted, "start completes despite a backend throwing in init");
            Assert.AreEqual(1, b.InitializeCalls);

            service.LogEvent("e");
            Assert.AreEqual(1, b.Events.Count, "service usable after a failed init");
        }

        // 15
        [Test]
        public async Task Flush_CallsBackendFlush_WhenReady()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.SetConsent(true);
            await service.StartAsync();

            service.Flush();

            Assert.AreEqual(1, backend.FlushCalls);
        }

        // 16
        [Test]
        public void Flush_BeforeReady_NoOp()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            service.Flush(); // not started, no consent → no-op

            Assert.AreEqual(0, backend.FlushCalls);
        }

        // 17
        [Test]
        public void Consent_Property_Reflects_Transitions()
        {
            var backend = new FakeAnalyticsBackend();
            var service = NewService(backend);

            Assert.AreEqual(AnalyticsConsent.Unknown, service.Consent);

            service.SetConsent(true);
            Assert.AreEqual(AnalyticsConsent.Granted, service.Consent);

            service.SetConsent(false);
            Assert.AreEqual(AnalyticsConsent.Denied, service.Consent);
        }
    }
}
