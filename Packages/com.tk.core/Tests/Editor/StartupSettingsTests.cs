using NUnit.Framework;
using TK.Core.App;
using UnityEngine;

namespace TK.Core.Tests
{
    [TestFixture]
    public class StartupSettingsTests
    {
        private static FrameRateProfile Profile(FrameRateMode mode, int fixedFps = 60) =>
            new() { mode = mode, fixedFps = fixedFps };

        // ---- frame rate ----

        [Test]
        public void PlatformDefault_WritesNothing_AtEveryRefreshRate()
        {
            foreach (var hz in new double[] { 60, 90, 120 })
            {
                Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.PlatformDefault), hz),
                    Is.Null, $"{hz} Hz");
            }
        }

        [Test]
        public void Fixed_UsesTheProfileValue()
        {
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.Fixed, 60), 120), Is.EqualTo(60));
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.Fixed, 30), 60), Is.EqualTo(30));
        }

        [TestCase(120.0, 120)]
        [TestCase(90.0, 90)]
        [TestCase(60.0, 60)]
        public void MatchRefreshRate_FollowsThePanel(double hz, int expected)
        {
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.MatchRefreshRate), hz),
                Is.EqualTo(expected));
        }

        [TestCase(120.0, 60)]
        [TestCase(90.0, 45)]
        [TestCase(60.0, 30)]
        public void HalfRefreshRate_HalvesThePanel(double hz, int expected)
        {
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.HalfRefreshRate), hz),
                Is.EqualTo(expected));
        }

        [Test]
        public void NinetyHertz_IsWhyTheRefreshModesExist()
        {
            // 60 divides evenly into 120 Hz and 60 Hz, but not into 90 — a fixed 60 beats against a
            // 90 Hz panel. These two modes are the way out, so the 90 Hz row is asserted on its own.
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.MatchRefreshRate), 90), Is.EqualTo(90));
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.HalfRefreshRate), 90), Is.EqualTo(45));
        }

        [Test]
        public void DegenerateInput_StillYieldsAUsableRate()
        {
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.MatchRefreshRate), 0), Is.EqualTo(1));
            Assert.That(StartupSettings.ResolveTargetFrameRate(Profile(FrameRateMode.Fixed, 0), 60), Is.EqualTo(1));
        }

        // ---- sleep timeout ----

        [Test]
        public void SleepTimeout_IsTriState()
        {
            Assert.That(StartupSettings.ResolveSleepTimeout(SleepTimeoutPolicy.LeaveDefault), Is.Null);
            Assert.That(StartupSettings.ResolveSleepTimeout(SleepTimeoutPolicy.NeverSleep),
                Is.EqualTo(SleepTimeout.NeverSleep));
            Assert.That(StartupSettings.ResolveSleepTimeout(SleepTimeoutPolicy.SystemSetting),
                Is.EqualTo(SleepTimeout.SystemSetting));
        }

        // ---- logs ----

        [Test]
        public void LeaveDefault_NeverDisablesLogs()
        {
            Assert.That(StartupSettings.ShouldDisableLogs(LogPolicy.LeaveDefault, isTestBuild: false, isPlayerBuild: true),
                Is.False);
        }

        [Test]
        public void DisableInReleaseBuilds_KeepsTestBuildLogs()
        {
            // "Test build" is a Development Build *or* a build compiled with TK_TEST_BUILD, so a
            // pipeline that marks its test builds with a define is covered too. The bug this policy
            // exists to fix: the old flag keyed only on "not the Editor", so a test build lost its logs
            // as well — which is how a package's device-only failure stayed invisible where you look.
            Assert.That(StartupSettings.ShouldDisableLogs(LogPolicy.DisableInReleaseBuilds, isTestBuild: true, isPlayerBuild: true),
                Is.False, "a development build must keep its logs");
            Assert.That(StartupSettings.ShouldDisableLogs(LogPolicy.DisableInReleaseBuilds, isTestBuild: false, isPlayerBuild: true),
                Is.True, "a release build must lose them");
        }

        [Test]
        public void DisableInAllPlayerBuilds_TakesTestBuildsToo()
        {
            Assert.That(StartupSettings.ShouldDisableLogs(LogPolicy.DisableInAllPlayerBuilds, isTestBuild: true, isPlayerBuild: true),
                Is.True);
            Assert.That(StartupSettings.ShouldDisableLogs(LogPolicy.DisableInAllPlayerBuilds, isTestBuild: false, isPlayerBuild: true),
                Is.True);
        }

        [Test]
        public void TheEditorNeverLosesItsLogs_UnderAnyPolicy()
        {
            foreach (LogPolicy policy in System.Enum.GetValues(typeof(LogPolicy)))
            {
                Assert.That(StartupSettings.ShouldDisableLogs(policy, isTestBuild: true, isPlayerBuild: false),
                    Is.False, policy.ToString());
            }
        }

        // ---- defaults ----

        [Test]
        public void FreshAsset_ComesUpWithTheDocumentedDefaults()
        {
            var settings = ScriptableObject.CreateInstance<StartupSettings>();
            try
            {
                Assert.That(settings.Mobile.mode, Is.EqualTo(FrameRateMode.Fixed));
                Assert.That(settings.Mobile.fixedFps, Is.EqualTo(60));
                Assert.That(settings.Standalone.mode, Is.EqualTo(FrameRateMode.PlatformDefault));
                Assert.That(settings.SleepPolicy, Is.EqualTo(SleepTimeoutPolicy.NeverSleep));
                Assert.That(settings.Logs, Is.EqualTo(LogPolicy.DisableInReleaseBuilds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
