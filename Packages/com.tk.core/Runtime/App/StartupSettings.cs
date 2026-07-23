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

        [SerializeField] private SleepTimeoutPolicy sleepTimeout = SleepTimeoutPolicy.NeverSleep;

        [SerializeField] private LogPolicy logs = LogPolicy.DisableInReleaseBuilds;

        public FrameRateProfile Mobile => mobile;
        public FrameRateProfile Standalone => standalone;
        public SleepTimeoutPolicy SleepPolicy => sleepTimeout;
        public LogPolicy Logs => logs;

        /// <summary>
        /// The asset startup applied, or null when none was found — in which case nothing was written
        /// and every platform default stands. <c>AppBootstrapper</c> reads this to decide whether its
        /// own deprecated log flag should still act as a fallback.
        /// </summary>
        public static StartupSettings Active { get; private set; }

        /// <summary>
        /// Which profile applies. Keyed on the active build target (the UNITY_ANDROID / UNITY_IOS
        /// defines) rather than <c>Application.isMobilePlatform</c>: those defines are set in the Editor
        /// too whenever a mobile target is selected, so pressing Play with an Android target exercises
        /// the same profile the device will get, and a consumer's PlayMode assertion on the applied rate
        /// holds. <c>isMobilePlatform</c> is false in the Editor, which would instead have applied the
        /// desktop profile to every in-editor session.
        /// </summary>
        private static bool UseMobileProfile =>
#if UNITY_ANDROID || UNITY_IOS
            true;
#else
            false;
#endif

        private static bool IsPlayerBuild =>
#if UNITY_EDITOR
            false;
#else
            true;
#endif

        /// <summary>
        /// Whether this build should be treated as a test build — one whose logs are worth keeping.
        ///
        /// Unity's own signal is <c>Debug.isDebugBuild</c> (the Development Build tick, always true in the
        /// Editor), but plenty of pipelines ship test builds without ticking it and mark them with a
        /// scripting define instead. Define <c>TK_TEST_BUILD</c> for those build configurations and they
        /// count as test builds too. A define rather than a setting on the asset because scripting defines
        /// are resolved at compile time — no string on a ScriptableObject can drive an <c>#if</c>.
        ///
        /// Requires the package to be compiled from source, which is how it ships.
        /// </summary>
        public static bool IsTestBuild =>
#if TK_TEST_BUILD
            true;
#else
            Debug.isDebugBuild;
#endif

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
        /// Whether startup should switch logging off. <see cref="LogPolicy.DisableInReleaseBuilds"/>
        /// deliberately leaves a test build's logs on — see <see cref="IsTestBuild"/> for what counts as
        /// one. That is the whole difference from <see cref="LogPolicy.DisableInAllPlayerBuilds"/>, and
        /// the bug in the field this replaces: it keyed only on "not the Editor", so it silenced test
        /// builds too and hid failures exactly where you go looking for them.
        /// </summary>
        public static bool ShouldDisableLogs(LogPolicy policy, bool isTestBuild, bool isPlayerBuild)
        {
            return policy switch
            {
                LogPolicy.DisableInReleaseBuilds => isPlayerBuild && !isTestBuild,
                LogPolicy.DisableInAllPlayerBuilds => isPlayerBuild,
                _ => false
            };
        }

        /// <summary>
        /// Applies this policy to the engine. Public so a PlayMode test can drive it directly.
        ///
        /// This runs in the Editor as well as in players: the values are written and therefore
        /// assertable in a PlayMode test. Note that the Game view has its own vsync, so the Editor's
        /// actual frame pacing is not governed by <c>Application.targetFrameRate</c> — measure frame
        /// times on a device, not in the Editor.
        /// </summary>
        public void Apply()
        {
            Active = this;

            var profile = UseMobileProfile ? mobile : standalone;
            var targetFrameRate = ResolveTargetFrameRate(profile, Screen.currentResolution.refreshRateRatio.value);
            if (targetFrameRate.HasValue) Application.targetFrameRate = targetFrameRate.Value;

            var sleep = ResolveSleepTimeout(sleepTimeout);
            if (sleep.HasValue) Screen.sleepTimeout = sleep.Value;

            if (ShouldDisableLogs(logs, IsTestBuild, IsPlayerBuild)) Debug.unityLogger.logEnabled = false;
        }

        /// <summary>
        /// Applied before the splash screen — the whole point of the asset. AppBootstrapper.Start()
        /// runs after the engine splash is already up, so a frame rate set there leaves the splash and
        /// the first frames at the platform default.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void LoadAndApply()
        {
            Active = null;   // domain-reload-off safety, same pattern as UIManager and SceneLoader

            var settings = Resources.Load<StartupSettings>(RESOURCES_NAME);
            if (settings == null)
            {
                // Writing nothing is a legitimate choice, so this is informational rather than a warning.
                // But it is said out loud, because an asset that is misnamed or sitting outside a
                // Resources folder produces exactly the same silence as a deliberate opt-out — and a
                // silent no-op is the one failure mode nobody notices. Debug builds only: a project that
                // genuinely wants platform defaults should not pay for this in release.
                if (IsTestBuild)
                {
                    Debug.Log($"[TK.Core] No '{RESOURCES_NAME}' asset found in a Resources folder — platform " +
                              "defaults stand (on mobile that leaves targetFrameRate at 30). If that is not " +
                              "intended, create one via Assets → Create → TK → Startup Settings and place it " +
                              $"in a Resources folder named '{RESOURCES_NAME}'.");
                }

                return;
            }

            settings.Apply();
        }
    }
}
