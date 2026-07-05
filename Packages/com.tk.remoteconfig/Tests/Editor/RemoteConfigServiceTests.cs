using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace TK.RemoteConfig.Tests
{
    [TestFixture]
    public sealed class RemoteConfigServiceTests
    {
        private static RemoteConfigService NewService(
            FakeRemoteConfigBackend backend, RemoteConfigOptions options = null)
            => new RemoteConfigService(backend, options);

        [TearDown]
        public void TearDown()
        {
            // Session overrides are process-static — clear between tests so they never leak.
            RemoteConfigDebug.ClearAll();
        }

        // 1
        [Test]
        public void Reads_BeforeInitialize_ReturnDefaults()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            Assert.IsFalse(rc.IsSafeToRead);
            Assert.AreEqual(7, rc.GetInt("i", 7));
            Assert.AreEqual("def", rc.GetString("s", "def"));
            Assert.IsTrue(rc.GetBool("b", true));
            Assert.AreEqual(1.5d, rc.GetDouble("d", 1.5d));
            Assert.AreEqual(2.5f, rc.GetFloat("f", 2.5f));
            Assert.AreEqual(9L, rc.GetLong("l", 9L));
        }

        // 2
        [Test]
        public async Task Initialize_SetsSafeToRead()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            await rc.InitializeAsync();

            Assert.IsTrue(rc.IsSafeToRead);
            Assert.AreEqual(1, backend.InitializeCalls);
            // No typed factories exist in this task, so the defaults dict may be empty —
            // assert it was passed through (non-null), not any specific registered key.
            Assert.IsNotNull(backend.ReceivedDefaults);
        }

        // 3
        [Test]
        public async Task Reads_AfterActivate_ReflectBackendValues()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("a", 7);
            backend.SetString("s", "x");
            var rc = NewService(backend);

            await rc.InitializeAsync();

            Assert.AreEqual(7, rc.GetInt("a", 0));
            Assert.AreEqual("x", rc.GetString("s", "d"));
        }

        // 4
        [Test]
        public async Task Reads_MissingKey_ReturnDefault_AfterReady()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            await rc.InitializeAsync();

            Assert.AreEqual(42, rc.GetInt("nope", 42));
        }

        // 5
        [Test]
        public async Task Initialize_SingleFlight()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            await rc.InitializeAsync();
            await rc.InitializeAsync();

            Assert.AreEqual(1, backend.InitializeCalls);
            Assert.AreEqual(1, backend.FetchCalls);
        }

        // 6
        [Test]
        public async Task Initialize_BackendThrows_StaysUnsafe_NoThrow()
        {
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("fake: init threw"));

            var backend = new FakeRemoteConfigBackend { ThrowOnInit = true };
            var rc = NewService(backend);

            await rc.InitializeAsync(); // must not throw

            Assert.IsFalse(rc.IsSafeToRead);
            Assert.IsFalse(rc.IsReady);
            Assert.AreEqual(5, rc.GetInt("x", 5)); // reads fall back to defaults
        }

        // 7
        [Test]
        public async Task Initialize_FetchFails_SafeToReadStillTrue_NotReady()
        {
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("fake: fetch failed"));

            var backend = new FakeRemoteConfigBackend { FailFetch = true };
            var rc = NewService(backend);

            await rc.InitializeAsync();

            Assert.IsTrue(rc.IsSafeToRead); // defaults registered; init itself succeeded
            Assert.IsFalse(rc.IsReady); // first fetch failed, so still not ready
            Assert.AreEqual(3, rc.GetInt("x", 3)); // missing key → default
        }

        // 8
        [Test]
        public async Task OnReady_FiresAfterInit()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            var count = 0;
            rc.OnReady += () => count++;

            await rc.InitializeAsync();

            Assert.AreEqual(1, count);
        }

        // 9
        [Test]
        public async Task OnReady_Latch_FiresImmediatelyWhenAlreadyReady()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            await rc.InitializeAsync();

            var count = 0;
            rc.OnReady += () => count++; // subscribing after ready invokes immediately
            Assert.AreEqual(1, count);

            await rc.RefreshAsync(); // a later refresh must NOT re-fire the latch
            Assert.AreEqual(1, count);
        }

        // 10
        [Test]
        public async Task OnChanged_FiresOnActivation()
        {
            var backend = new FakeRemoteConfigBackend { NextFetchActivates = true };
            var rc = NewService(backend);

            var count = 0;
            rc.OnChanged += () => count++;

            await rc.InitializeAsync();
            Assert.AreEqual(1, count);

            await rc.RefreshAsync();
            Assert.AreEqual(2, count);
        }

        // 11
        [Test]
        public async Task OnChanged_NotFired_WhenNoNewActivation()
        {
            var backend = new FakeRemoteConfigBackend { NextFetchActivates = false };
            var rc = NewService(backend);

            var changed = 0;
            var ready = 0;
            rc.OnChanged += () => changed++;
            rc.OnReady += () => ready++;

            await rc.InitializeAsync();

            Assert.AreEqual(0, changed); // fetch did not activate new values
            Assert.IsTrue(rc.IsReady); // but a successful (non-activating) fetch still marks ready
            Assert.AreEqual(1, ready); // and the latch fired once
        }

        // 12
        [Test]
        public async Task Refresh_BeforeInit_ReturnsFalse_Warns()
        {
            LogAssert.Expect(UnityEngine.LogType.Warning,
                new Regex("RefreshAsync called before InitializeAsync"));

            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            var result = await rc.RefreshAsync();

            Assert.IsFalse(result);
            Assert.AreEqual(0, backend.FetchCalls);
        }

        // 13
        [Test]
        public async Task Refresh_ReturnsActivationResult()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            await rc.InitializeAsync();

            backend.NextFetchActivates = false;
            Assert.IsFalse(await rc.RefreshAsync());

            backend.NextFetchActivates = true;
            Assert.IsTrue(await rc.RefreshAsync());
        }

        // 14
        [Test]
        public async Task Int_OverflowFromLong_UncheckedCast()
        {
            var backend = new FakeRemoteConfigBackend();
            var big = (long)int.MaxValue + 1;
            backend.SetLong("big", big);
            var rc = NewService(backend);

            await rc.InitializeAsync();

            // Contract: GetInt truncates via unchecked((int)v) — int.MaxValue+1 wraps to int.MinValue.
            Assert.AreEqual(unchecked((int)big), rc.GetInt("big", 0));
            Assert.AreEqual(int.MinValue, rc.GetInt("big", 0));
        }
    }
}
