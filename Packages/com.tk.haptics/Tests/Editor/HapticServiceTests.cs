using NUnit.Framework;

namespace TK.Haptics.Tests
{
    [TestFixture]
    public class HapticServiceTests
    {
        private FakeHapticBackend _backend;

        private HapticService NewService()
        {
            _backend = new FakeHapticBackend();
            return new HapticService(_backend);
        }

        [Test]
        public void Disabled_FiresNothing()
        {
            var haptics = NewService();
            haptics.Enabled = false;

            haptics.Impact(HapticImpact.Heavy);
            haptics.Selection();
            haptics.Notification(HapticNotification.Error);

            CollectionAssert.IsEmpty(_backend.Calls);
        }

        [Test]
        public void SystemTouchVibrationDisabled_DefaultsFalse_AndForwardsLiveFromTheBackend()
        {
            var haptics = NewService();
            Assert.That(haptics.SystemTouchVibrationDisabled, Is.False, "default is false");

            // Live forwarding matters for the same reason as IsSupported: the OS setting can change
            // mid-session, so a settings screen must see the current answer, not a boot-time snapshot.
            _backend.TouchVibrationDisabled = true;
            Assert.That(haptics.SystemTouchVibrationDisabled, Is.True);

            _backend.TouchVibrationDisabled = false;
            Assert.That(haptics.SystemTouchVibrationDisabled, Is.False);
        }

        [Test]
        public void IsSupported_IsReadLive_SoARuntimeDemotionReachesTheGame()
        {
            var haptics = NewService();
            Assert.That(haptics.IsSupported, Is.True);

            // The Android backend demotes itself when a vibrate() call is denied because the app was
            // built without android.permission.VIBRATE. That demotion has to reach the game's settings
            // screen, so IsSupported must be read from the backend on every access — never cached at
            // construction, which would leave a Vibration toggle switched on over a dead backend.
            _backend.Supported = false;

            Assert.That(haptics.IsSupported, Is.False);

            haptics.Selection();
            CollectionAssert.IsEmpty(_backend.Calls, "an unsupported backend must stop receiving dispatches");
        }

        [Test]
        public void Enabled_DispatchesEachTaxonomyValue()
        {
            var haptics = NewService();

            haptics.Impact(HapticImpact.Light);
            haptics.Impact(HapticImpact.Medium);
            haptics.Impact(HapticImpact.Heavy);
            haptics.Selection();
            haptics.Notification(HapticNotification.Success);
            haptics.Notification(HapticNotification.Warning);
            haptics.Notification(HapticNotification.Error);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Impact:Light", "Impact:Medium", "Impact:Heavy", "Selection",
                    "Notification:Success", "Notification:Warning", "Notification:Error"
                },
                _backend.Calls);
        }

        [Test]
        public void Throttle_DropsRapidSameType_AllowsDifferentType()
        {
            var haptics = NewService();

            haptics.Impact(HapticImpact.Light);
            haptics.Impact(HapticImpact.Light); // same type, same frame → within the window → dropped
            haptics.Selection();                // different type → fires

            CollectionAssert.AreEqual(new[] { "Impact:Light", "Selection" }, _backend.Calls);
        }

        [Test]
        public void Throttle_DifferentImpactStrengths_AreIndependent()
        {
            var haptics = NewService();

            haptics.Impact(HapticImpact.Light);
            haptics.Impact(HapticImpact.Heavy); // different strength = different throttle key → fires

            CollectionAssert.AreEqual(new[] { "Impact:Light", "Impact:Heavy" }, _backend.Calls);
        }

        [Test]
        public void ThrottleZero_AllowsRapidRepeat()
        {
            var haptics = NewService();
            haptics.HapticThrottleSeconds = 0f;

            haptics.Impact(HapticImpact.Light);
            haptics.Impact(HapticImpact.Light);

            Assert.AreEqual(2, _backend.Calls.Count);
        }

        [Test]
        public void UnsupportedBackend_FiresNothing()
        {
            var haptics = NewService();
            _backend.Supported = false;

            haptics.Impact(HapticImpact.Heavy);

            Assert.IsFalse(haptics.IsSupported);
            CollectionAssert.IsEmpty(_backend.Calls);
        }

        [Test]
        public void Changed_FiresOnRealToggle_NotNoOp()
        {
            var haptics = NewService();
            var count = 0;
            haptics.Changed += () => count++;

            haptics.Enabled = false;
            Assert.AreEqual(1, count);

            haptics.Enabled = false; // no-op
            Assert.AreEqual(1, count);

            haptics.Enabled = true;
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Enabled_DefaultsTrue_AndIsRuntimeState()
        {
            var haptics = NewService();

            Assert.IsTrue(haptics.Enabled, "Haptics default on; the game owns/persists the value.");
            haptics.Enabled = false;
            Assert.IsFalse(haptics.Enabled);
        }

        [Test]
        public void IsSupported_ReflectsTheBackend()
        {
            var haptics = NewService();
            Assert.IsTrue(haptics.IsSupported);

            _backend.Supported = false;
            Assert.IsFalse(haptics.IsSupported);
        }

        [Test]
        public void DefaultBackend_IsNoOp_InTheEditor()
        {
            // No backend injected → the platform pick, which is the no-op in the Editor.
            var haptics = new HapticService();

            Assert.IsFalse(haptics.IsSupported, "The Editor has no haptics.");
            Assert.DoesNotThrow(() => haptics.Impact(HapticImpact.Heavy));
        }
    }
}
