using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TK.RemoteConfig.Tests
{
    [TestFixture]
    public sealed class FakeRemoteConfigBackendTests
    {
        [Test]
        public async Task InitializeAsync_RecordsDefaults_AndReady()
        {
            var backend = new FakeRemoteConfigBackend();
            var defaults = new Dictionary<string, object> { ["k"] = 1L };

            await backend.InitializeAsync(defaults);

            Assert.AreSame(defaults, backend.ReceivedDefaults);
            Assert.IsTrue(backend.IsReady);
            Assert.AreEqual(1, backend.InitializeCalls);
        }

        [Test]
        public void InitializeAsync_ThrowKnob_Throws()
        {
            var backend = new FakeRemoteConfigBackend { ThrowOnInit = true };

            Assert.ThrowsAsync<System.InvalidOperationException>(
                async () => await backend.InitializeAsync(new Dictionary<string, object>()));
        }

        [Test]
        public void TryGet_TypedHitAndMiss()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("a", 5);

            Assert.IsTrue(backend.TryGetLong("a", out var hit));
            Assert.AreEqual(5, hit);

            Assert.IsFalse(backend.TryGetLong("b", out var miss));
            Assert.AreEqual(0, miss);

            // "a" holds a long, so reading it as a string is a type miss.
            Assert.IsFalse(backend.TryGetString("a", out var wrongType));
            Assert.IsNull(wrongType);
        }

        [Test]
        public async Task FetchAndActivate_ReturnsKnob_AndCounts()
        {
            var backend = new FakeRemoteConfigBackend { NextFetchActivates = false };

            var activated = await backend.FetchAndActivateAsync();

            Assert.IsFalse(activated);
            Assert.AreEqual(1, backend.FetchCalls);
        }
    }
}
