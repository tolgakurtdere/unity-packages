using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Project-wide startup platform policy: target frame rate, sleep timeout and log suppression.
    ///
    /// Why an asset and not fields on a scene component: these are applied before the splash screen,
    /// when no scene exists yet, so nothing can hold an inspector reference to them. Put the asset in a
    /// <c>Resources</c> folder under the name <see cref="RESOURCES_NAME"/> and startup finds it. With no
    /// asset present the package writes nothing at all and every platform default stands — that is the
    /// documented way to opt out, so no consumer is forced onto a frame rate.
    ///
    /// The decision logic lives in the pure static resolvers below, so the whole mode matrix is testable
    /// without a device and without mutating global engine state.
    /// </summary>
    [CreateAssetMenu(fileName = RESOURCES_NAME, menuName = "TK/Startup Settings")]
    public sealed class StartupSettings : ScriptableObject
    {
        /// <summary>The Resources name startup loads this asset from.</summary>
        public const string RESOURCES_NAME = "TKStartupSettings";

        [Tooltip("Applied when Application.isMobilePlatform is true.")]
        [SerializeField] private FrameRateProfile mobile = new() { mode = FrameRateMode.Fixed, fixedFps = 60 };

        [Tooltip("Applied on every non-mobile target (desktop, console, and the Editor).")]
        [SerializeField] private FrameRateProfile standalone = new() { mode = FrameRateMode.PlatformDefault, fixedFps = 60 };

        [SerializeField] private SleepTimeoutPolicy sleepTimeout = SleepTimeoutPolicy.LeaveDefault;

        [SerializeField] private LogPolicy logs = LogPolicy.DisableInReleaseBuilds;

        public FrameRateProfile Mobile => mobile;
        public FrameRateProfile Standalone => standalone;
        public SleepTimeoutPolicy SleepPolicy => sleepTimeout;
        public LogPolicy Logs => logs;

        /// <summary>
        /// The frame rate to write, or null when the profile leaves the platform default alone.
        /// Null rather than -1 as the "write nothing" sentinel: -1 is itself a meaningful value for
        /// <c>Application.targetFrameRate</c>, so it could not be told apart from "don't touch this".
        /// </summary>
        public static int? ResolveTargetFrameRate(FrameRateProfile profile, double refreshRateHz)
        {
            return profile.mode switch
            {
                FrameRateMode.Fixed => (int?)Mathf.Max(1, profile.fixedFps),
                FrameRateMode.MatchRefreshRate => Mathf.Max(1, (int)System.Math.Round(refreshRateHz)),
                FrameRateMode.HalfRefreshRate => Mathf.Max(1, (int)System.Math.Round(refreshRateHz / 2d)),
                _ => null
            };
        }

        /// <summary>The value to write to <c>Screen.sleepTimeout</c>, or null to leave it alone.</summary>
        public static int? ResolveSleepTimeout(SleepTimeoutPolicy policy)
        {
            return policy switch
            {
                SleepTimeoutPolicy.NeverSleep => (int?)UnityEngine.SleepTimeout.NeverSleep,
                SleepTimeoutPolicy.SystemSetting => UnityEngine.SleepTimeout.SystemSetting,
                _ => null
            };
        }

        /// <summary>
        /// Whether startup should switch logging off. A development build counts as a debug build, so
        /// <see cref="LogPolicy.DisableInReleaseBuilds"/> deliberately leaves a test build's logs on.
        /// That is the whole difference from <see cref="LogPolicy.DisableInAllPlayerBuilds"/>, and the
        /// bug in the field this replaces: it keyed only on "not the Editor", so it silenced
        /// development builds too and hid failures exactly where you go looking for them.
        /// </summary>
        public static bool ShouldDisableLogs(LogPolicy policy, bool isDebugBuild, bool isPlayerBuild)
        {
            return policy switch
            {
                LogPolicy.DisableInReleaseBuilds => isPlayerBuild && !isDebugBuild,
                LogPolicy.DisableInAllPlayerBuilds => isPlayerBuild,
                _ => false
            };
        }
    }
}
