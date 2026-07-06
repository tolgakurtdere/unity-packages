using NUnit.Framework;
using TK.Core.UI;

namespace TK.Core.Tests
{
    [TestFixture]
    public class TabTransitionSettingsTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void CalculateDuration_OneStep_IsMinDuration()
        {
            var settings = new TabTransitionSettings(); // defaults: min 0.2, max 0.5, multiplier 0.5

            Assert.AreEqual(settings.MinDuration, settings.CalculateDuration(1), Tolerance);
        }

        [Test]
        public void CalculateDuration_ExtraStepsScaleByMultiplier()
        {
            var settings = new TabTransitionSettings();

            // min + (steps-1) * min * multiplier = 0.2 + 1 * 0.2 * 0.5
            Assert.AreEqual(0.3f, settings.CalculateDuration(2), Tolerance);
        }

        [Test]
        public void CalculateDuration_ClampsToMaxDuration()
        {
            var settings = new TabTransitionSettings();

            Assert.AreEqual(settings.MaxDuration, settings.CalculateDuration(10), Tolerance);
        }

        [Test]
        public void CalculateDuration_CoercesNonPositiveStepsToOne()
        {
            var settings = new TabTransitionSettings();

            Assert.AreEqual(settings.MinDuration, settings.CalculateDuration(0), Tolerance);
            Assert.AreEqual(settings.MinDuration, settings.CalculateDuration(-5), Tolerance);
        }

        [Test]
        public void Evaluate_ClampsNormalizedTime()
        {
            var settings = new TabTransitionSettings();

            Assert.AreEqual(0f, settings.Evaluate(-1f), Tolerance, "Below 0 must evaluate at curve start.");
            Assert.AreEqual(1f, settings.Evaluate(2f), Tolerance, "Above 1 must evaluate at curve end.");
        }

        [Test]
        public void Default_IsAvailableWithSaneInvariants()
        {
            var settings = TabTransitionSettings.Default;

            Assert.IsNotNull(settings);
            Assert.GreaterOrEqual(settings.MaxDuration, settings.MinDuration);
            Assert.Greater(settings.MinDuration, 0f);
            Assert.IsTrue(settings.UseUnscaledTime, "Default must keep sliding while time scale is paused.");
        }
    }
}
