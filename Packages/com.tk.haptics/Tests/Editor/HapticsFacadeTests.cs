using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.Haptics.Tests
{
    [TestFixture]
    public class HapticsFacadeTests
    {
        [SetUp]
        public void SetUp()
        {
            // Start every test unbound WITH a fresh warn-once flag. Only Bind resets that flag, and
            // NUnit's in-fixture order isn't guaranteed — two tests expecting the unbound warning must
            // not depend on which ran first.
            Haptics.Bind(new HapticService(backend: new FakeHapticBackend()));
            Haptics.Unbind(Haptics.Service);
        }

        [TearDown]
        public void TearDown() => Haptics.Unbind(Haptics.Service);

        [Test]
        public void Bind_RoutesCallsToTheService()
        {
            var backend = new FakeHapticBackend();
            var service = new HapticService(backend: backend);
            Haptics.Bind(service);

            Haptics.Selection();
            Haptics.Impact(HapticImpact.Heavy);

            CollectionAssert.AreEqual(new[] { "Selection", "Impact:Heavy" }, backend.Calls);
        }

        [Test]
        public void UnboundCalls_WarnOnceAndNoOp()
        {
            LogAssert.Expect(LogType.Warning,
                "[Haptics] No HapticService bound — call Haptics.Bind(service) at your composition root.");
            Haptics.Selection();
            Haptics.Impact(HapticImpact.Light); // second unbound call stays silent

            Assert.IsFalse(Haptics.IsSupported, "Unbound reports unsupported.");
            Assert.IsTrue(Haptics.Enabled, "Unbound getter falls back to the default.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Unbind_OnlyDetachesTheBoundInstance()
        {
            var a = new HapticService(backend: new FakeHapticBackend());
            var b = new HapticService(backend: new FakeHapticBackend());
            Haptics.Bind(a);

            Haptics.Unbind(b);
            Assert.AreSame(a, Haptics.Service, "Unbinding a different instance is a no-op.");

            Haptics.Unbind(a);
            Assert.IsNull(Haptics.Service);
        }

        [Test]
        public void AdvisoryAndBypass_ForwardThroughTheFacade()
        {
            var backend = new FakeHapticBackend { TouchVibrationDisabled = true };
            Haptics.Bind(new HapticService(backend: backend));

            Assert.IsTrue(Haptics.SystemTouchVibrationDisabled);

            Haptics.BypassSystemVibrationSetting = true;
            Assert.IsTrue(backend.BypassSystemVibrationSetting, "the façade setter must reach the backend");
            Assert.IsTrue(Haptics.BypassSystemVibrationSetting);
        }

        [Test]
        public void Unbound_AdvisoryIsFalse_AndBypassSetIsASafeNoOp()
        {
            // The getters read s_service directly (no Resolve), so they must stay warning-free.
            Assert.IsFalse(Haptics.SystemTouchVibrationDisabled, "unbound advisory is false");
            Assert.IsFalse(Haptics.BypassSystemVibrationSetting, "unbound bypass reads false");

            // The setter goes through Resolve(), so the first unbound touch warns once (SetUp
            // guarantees a fresh warn flag) — same contract as every other unbound façade call.
            LogAssert.Expect(LogType.Warning,
                "[Haptics] No HapticService bound — call Haptics.Bind(service) at your composition root.");
            Haptics.BypassSystemVibrationSetting = true;   // no service: swallowed
            Assert.IsFalse(Haptics.BypassSystemVibrationSetting);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void IsSupported_ReflectsTheBoundService()
        {
            var backend = new FakeHapticBackend { Supported = true };
            Haptics.Bind(new HapticService(backend: backend));

            Assert.IsTrue(Haptics.IsSupported);
        }
    }
}
